using Pulumi;
using Pulumi.Gcp.CloudRun;
using Pulumi.Gcp.CloudRun.Inputs;
using System;
using System.Collections.Generic;
using System.IO;

class MyStack : Stack
{
    const string LOCATION = "us-west1";
    const string PROJECT = "austin-pulumi-test";
    const string REPO_ID = "my-id";

    readonly string mRepoRoot;

    static string getRepoRoot()
    {
        string? repoRoot = Path.GetDirectoryName(typeof(MyStack).Assembly.Location);
        while (repoRoot is object && !File.Exists(Path.Combine(repoRoot, "Pulumi.yaml")))
        {
            repoRoot = Path.GetDirectoryName(repoRoot);
        }
        if (repoRoot == null)
            throw new Exception("Could not find Pulumi.yaml");
        return repoRoot;
    }

    public MyStack()
    {
        // TODO: is there a Pulumi-supported way of finding the directory are being run from?
        mRepoRoot = getRepoRoot();

        var artifacts = new Pulumi.Gcp.ArtifactRegistry.Repository("my-artifacts", new Pulumi.Gcp.ArtifactRegistry.RepositoryArgs()
        {
            Format = "DOCKER",
            Location = LOCATION,
            Project = PROJECT,
            RepositoryId = REPO_ID,
        });

        Pulumi.Docker.Image frontendImage = createDockerImage("frontend-app", "FrontendWebApp");
        Pulumi.Docker.Image backendImage = createDockerImage("backend-app", "BackendService");

        var frontEndServiceAccount = new Pulumi.Gcp.ServiceAccount.Account("frontend-service-account", new Pulumi.Gcp.ServiceAccount.AccountArgs()
        {
            AccountId = "frontend",
            Project = PROJECT,
        });

        var backendService = createService("backend-service", backendImage);
        var frontendService = createService("frontend-service", frontendImage, serviceAccountName: frontEndServiceAccount.Email, envs: new InputList<ServiceTemplateSpecContainerEnvArgs>
        {
            backendService.Statuses.Apply(list => new ServiceTemplateSpecContainerEnvArgs() { Name = "GrpcService__BackendUri", Value = list[0].Url! }),
            new ServiceTemplateSpecContainerEnvArgs() { Name = "CloudRun__ServiceIdentity", Value = frontEndServiceAccount.Email },
        });

        var binding = new IamBinding("frontend-to-backend-binding", new IamBindingArgs()
        {
            Project = PROJECT,
            Location = LOCATION,
            Service = backendService.Name,
            Role = "roles/run.invoker",
            Members = new InputList<string>()
            {
                frontEndServiceAccount.Email.Apply(email => $"serviceAccount:{email}"),
            },
        });

        var frontendPublic = new IamMember("frontend-service-public-invoker", new IamMemberArgs()
        {
            Service = frontendService.Name,
            Member = "allUsers",
            Role = "roles/run.invoker",
            Project = PROJECT,
            Location = LOCATION,
        });

        this.ServiceAccount = frontEndServiceAccount.Email;
        this.ServiceUrl = backendService.Statuses.Apply(list => list[0].Url);
        this.FrontendUrl = frontendService.Statuses.Apply(list => list[0].Url);
    }

    [Output]
    public Output<string?> ServiceUrl { get; set; }

    [Output]
    public Output<string?> FrontendUrl { get; set; }

    [Output]
    public Output<string> ServiceAccount { get; set; }

    private Service createService(string name, Pulumi.Docker.Image image, Input<string>? serviceAccountName = null, InputList<ServiceTemplateSpecContainerEnvArgs>? envs = null)
    {
        var containerArgs = new ServiceTemplateSpecContainerArgs()
        {
            Image = image.ImageName,
        };
        if (envs is object)
        {
            containerArgs.Envs = envs;
        }


        var spec = new ServiceTemplateSpecArgs()
        {
            Containers = new List<ServiceTemplateSpecContainerArgs>()
            {
                containerArgs,
            },
        };
        if (serviceAccountName != null)
        {
            spec.ServiceAccountName = serviceAccountName;
        }

        return new Service(name, new ServiceArgs()
        {
            Location = LOCATION,
            Project = PROJECT,
            Template = new ServiceTemplateArgs()
            {
                Spec = spec,
            },
            Traffics = new List<ServiceTrafficArgs>
            {
                new ServiceTrafficArgs()
                {
                    LatestRevision = true,
                    Percent = 100,
                },
            },
        });
    }

    private Pulumi.Docker.Image createDockerImage(string imageName, string folderName)
    {
        var frontendImage = new Pulumi.Docker.Image(imageName, new Pulumi.Docker.ImageArgs
        {
            Build = new Pulumi.Docker.DockerBuild()
            {
                Context = mRepoRoot,
                Dockerfile = Path.Combine(mRepoRoot, folderName, "Dockerfile"),
            },
            // TODO: take a Repository object to calculate the image name
            ImageName = $"{LOCATION}-docker.pkg.dev/{PROJECT}/{REPO_ID}/{imageName}",
            // we assume this command has been run for authentication:
            // gcloud auth configure-docker us-west1-docker.pkg.dev
            // TODO: see if there is a way to get some credentials to pass to Docker
        });
        return frontendImage;
    }
}
