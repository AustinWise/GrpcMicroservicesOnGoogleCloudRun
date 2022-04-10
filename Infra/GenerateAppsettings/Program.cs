using InfraLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

var allServices = ServiceDefinition.GetAllServices();

var ports = new Dictionary<ServiceDefinition, int>();

int port = 8000;
foreach (var svc in allServices)
{
    ports.Add(svc, port++);
}

var repoRoot = RootFinder.GetRepoRoot();
foreach (var svc in allServices)
{
    var settings = new Dictionary<string, JToken>();
    settings["PORT"] = ports[svc];

    foreach (var dep in svc.Dependancies)
    {
        settings[$"GrpcService:{dep.Name}"] = $"http://localhost:{ports[dep]}/";
    }

    svc.UpdateAppSettings(repoRoot, settings);
}

Console.WriteLine("Done writing app settings for local hosting");
