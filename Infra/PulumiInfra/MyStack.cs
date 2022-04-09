using InfraLib;
using Pulumi;
using Pulumi.Gcp.CloudRun;
using Pulumi.Gcp.CloudRun.Inputs;
using Pulumi.Gcp.ServiceAccount;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

class MyStack : Stack
{
    const string LOCATION = "us-west1";
    const string PROJECT = "austin-pulumi-test";
    const string REPO_ID = "my-id";

    readonly string mRepoRoot;
    readonly List<ServiceDefinition> mServiceDefs;

    readonly Pulumi.Gcp.ArtifactRegistry.Repository mArtifacts;
    readonly Dictionary<ServiceDefinition, Service> mServices = new();
    readonly Dictionary<ServiceDefinition, Account> mAccounts = new();

    private Service GetOrCreateService(ServiceDefinition def)
    {
        Service? svc;
        if (mServices.TryGetValue(def, out svc))
            return svc;

        var account = mAccounts[def];
        var envs = new InputList<ServiceTemplateSpecContainerEnvArgs>();
        foreach (var dep in def.Dependancies)
        {
            envs.Add(GetOrCreateService(dep).Statuses.Apply(list => new ServiceTemplateSpecContainerEnvArgs() { Name = "GrpcService__" + dep.Name, Value = list[0].Url! }));
        }
        envs.Add(new ServiceTemplateSpecContainerEnvArgs() { Name = "CloudRun__ServiceIdentity", Value = account.Email });
        var img = createDockerImage(def.Name, def.RepoRelativePath);
        svc = createService(def.Name, img, account, envs);

        mServices.Add(def, svc);
        return svc;
    }

    public MyStack()
    {
        // TODO: is there a Pulumi-supported way of finding the directory are being run from?
        mRepoRoot = RootFinder.GetRepoRoot();
        mServiceDefs = ServiceDefinition.GetAllServices();

        mArtifacts = new Pulumi.Gcp.ArtifactRegistry.Repository("my-artifacts", new Pulumi.Gcp.ArtifactRegistry.RepositoryArgs()
        {
            Format = "DOCKER",
            Location = LOCATION,
            Project = PROJECT,
            RepositoryId = REPO_ID,
        });

        foreach (var svc in mServiceDefs)
        {
            mAccounts.Add(svc, new Account(svc.Name + "-account", new AccountArgs()
            {
                AccountId = svc.Name,
                Project = PROJECT,
            }));
        }

        foreach (var svc in mServiceDefs)
        {
            GetOrCreateService(svc);
        }

        foreach (var svc in mServiceDefs)
        {
            var account = mAccounts[svc];
            foreach (var dep in svc.Dependancies)
            {
                var binding = new IamBinding($"{svc.Name}-to-{dep.Name}-binding", new IamBindingArgs()
                {
                    Project = PROJECT,
                    Location = LOCATION,
                    Service = mServices[dep].Name,
                    Role = "roles/run.invoker",
                    Members = new InputList<string>()
                    {
                        account.Email.Apply(email => $"serviceAccount:{email}"),
                    },
                });

            }
        }

        foreach (var kvp in mServices)
        {
            if (kvp.Key.IsPublic)
            {
                new IamMember(kvp.Key.Name + "public-invoker", new IamMemberArgs()
                {
                    Service = kvp.Value.Name,
                    Member = "allUsers",
                    Role = "roles/run.invoker",
                    Project = PROJECT,
                    Location = LOCATION,
                });
            }
        }

        PublicUrls = Output.All(mServices.Where(kvp => kvp.Key.IsPublic).Select(kvp => kvp.Value.Statuses.Apply(s => s[0].Url)));
    }

    [Output]
    public Output<ImmutableArray<string?>> PublicUrls { get; set; }

    private Service createService(string name, Pulumi.Docker.Image image, Account serviceAccountName, InputList<ServiceTemplateSpecContainerEnvArgs> envs)
    {
        var containerArgs = new ServiceTemplateSpecContainerArgs()
        {
            Image = image.ImageName,
            Envs = envs,
        };

        var spec = new ServiceTemplateSpecArgs()
        {
            Containers = new List<ServiceTemplateSpecContainerArgs>()
            {
                containerArgs,
            },
            ServiceAccountName = serviceAccountName.Email,
        };

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
        var img = new Pulumi.Docker.Image(imageName, new Pulumi.Docker.ImageArgs
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
        return img;
    }
}
