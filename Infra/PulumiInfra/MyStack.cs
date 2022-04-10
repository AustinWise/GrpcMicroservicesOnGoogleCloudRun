using InfraLib;
using Pulumi;
using Pulumi.Gcp.CloudRun;
using Pulumi.Gcp.CloudRun.Inputs;
using Pulumi.Gcp.ServiceAccount;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

record ServiceInstance(ServiceDefinition Def, Account Account, Service Service)
{
}

class MyStack : Stack
{
    const string LOCATION = "us-west1";
    public const string PROJECT = "austin-pulumi-test";
    const string REPO_ID = "my-id";

    readonly string mRepoRoot;
    readonly List<ServiceDefinition> mServiceDefs;

    readonly Dictionary<ServiceDefinition, ServiceInstance> mServices = new();

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
            //   gcloud auth configure-docker us-west1-docker.pkg.dev
            // TODO: see if there is a way to get some credentials to pass to Docker
        });
        return img;
    }

    private ServiceInstance GetOrCreateService(ServiceDefinition def)
    {
        ServiceInstance? svc;
        if (mServices.TryGetValue(def, out svc))
            return svc;

        var account = new Account(def.Name + "-account", new AccountArgs()
        {
            AccountId = def.Name,
            Project = PROJECT,
        });

        var envs = new InputList<ServiceTemplateSpecContainerEnvArgs>();
        foreach (var dep in def.Dependancies)
        {
            envs.Add(GetOrCreateService(dep).Service.Statuses.Apply(list => new ServiceTemplateSpecContainerEnvArgs() { Name = "GrpcService__" + dep.Name, Value = list[0].Url! }));
        }
        envs.Add(new ServiceTemplateSpecContainerEnvArgs() { Name = "CloudRun__ServiceIdentity", Value = account.Email });

        var img = createDockerImage(def.Name, def.RepoRelativePath);
        var containerArgs = new ServiceTemplateSpecContainerArgs()
        {
            Image = img.ImageName,
            Envs = envs,
        };
        var spec = new ServiceTemplateSpecArgs()
        {
            Containers = new List<ServiceTemplateSpecContainerArgs>()
            {
                containerArgs,
            },
            ServiceAccountName = account.Email,
        };
        var service = new Service(def.Name, new ServiceArgs()
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

        svc = new ServiceInstance(def, account, service);
        mServices.Add(def, svc);
        return svc;
    }

    public MyStack()
    {
        // TODO: is there a Pulumi-supported way of finding the directory are being run from?
        mRepoRoot = RootFinder.GetRepoRoot();
        mServiceDefs = ServiceDefinition.GetAllServices();

        _ = new Pulumi.Gcp.ArtifactRegistry.Repository("my-artifacts", new Pulumi.Gcp.ArtifactRegistry.RepositoryArgs()
        {
            Format = "DOCKER",
            Location = LOCATION,
            Project = PROJECT,
            RepositoryId = REPO_ID,
        });

        foreach (var svc in mServiceDefs)
        {
            GetOrCreateService(svc);
        }

        foreach (var svc in mServices.Values)
        {
            foreach (var dep in svc.Def.Dependancies)
            {
                var binding = new IamBinding($"{svc.Def.Name}-to-{dep.Name}-binding", new IamBindingArgs()
                {
                    Project = PROJECT,
                    Location = LOCATION,
                    Service = mServices[dep].Service.Name,
                    Role = "roles/run.invoker",
                    Members = new InputList<string>()
                    {
                        svc.Account.Email.Apply(email => $"serviceAccount:{email}"),
                    },
                });

            }
        }

        foreach (var svc in mServices.Values)
        {
            if (svc.Def.IsPublic)
            {
                _ = new IamMember(svc.Def.Name + "public-invoker", new IamMemberArgs()
                {
                    Service = svc.Service.Name,
                    Member = "allUsers",
                    Role = "roles/run.invoker",
                    Project = PROJECT,
                    Location = LOCATION,
                });
            }
        }

        PublicUrls = Output.All(mServices.Values.Where(svc => svc.Def.IsPublic).Select(svc => svc.Service.Statuses.Apply(s => s[0].Url)));
    }

    [Output]
    public Output<ImmutableArray<string?>> PublicUrls { get; set; }
}
