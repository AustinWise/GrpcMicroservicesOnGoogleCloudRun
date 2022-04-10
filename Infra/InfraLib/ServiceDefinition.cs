using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;

namespace InfraLib
{
    public record ServiceDefinition(string Name, string RepoRelativePath, ReadOnlyCollection<ServiceDefinition> Dependancies, bool IsPublic = false)
    {
        static IEnumerable<ServiceDefinition> GetAllServicesCore()
        {
            var backend = new ServiceDefinition("backend-service", "BackendService", CreateList());
            yield return backend;

            yield return new ServiceDefinition("frontend-service", "FrontendWebApp", CreateList(backend), IsPublic: true);
        }

        static ReadOnlyCollection<ServiceDefinition> CreateList(params ServiceDefinition[] defs)
        {
            return new ReadOnlyCollection<ServiceDefinition>(defs);
        }

        static void ValidateLackOfCycles(Stack<ServiceDefinition> seen, ServiceDefinition node)
        {
            if (seen.Contains(node))
                throw new Exception("Cycle: " + string.Join(" -> ", seen.Select(n => n.Name)));

            seen.Push(node);
            foreach (var dep in node.Dependancies)
            {
                ValidateLackOfCycles(seen, dep);
            }
            seen.Pop();
        }

        public static List<ServiceDefinition> GetAllServices()
        {
            var ret = new List<ServiceDefinition>(GetAllServicesCore());

            var unqiueNames = new HashSet<string>();

            foreach (var n in ret)
            {
                if (!unqiueNames.Add(n.Name))
                    throw new Exception("Name not unique: " + n.Name);
            }

            foreach (var n in ret)
            {
                ValidateLackOfCycles(new Stack<ServiceDefinition>(), n);
            }

            return ret;
        }

        public void UpdateAppSettings(string repoRoot, Dictionary<string, JToken> appSettings)
        {
            var filePath = Path.Combine(repoRoot, RepoRelativePath, "appsettings.Development.json");

            JObject doc;
            if (File.Exists(filePath))
            {
                using var sr = new StreamReader(filePath);
                using var jr = new JsonTextReader(sr);
                doc = (JObject)JValue.Load(jr);
            }
            else
            {
                doc = new JObject();
            }

            foreach (var kvp in appSettings)
            {
                var path = kvp.Key.Split(':');

                JObject obj = doc;
                foreach (var p in path.Take(path.Length - 1))
                {
                    var nestedObj = (JObject?)obj[p];
                    if (nestedObj is null)
                    {
                        nestedObj = new JObject();
                        obj[p] = nestedObj;
                    }
                    obj = nestedObj;
                }

                obj[path.Last()] = kvp.Value;
            }

            using var sw = new StreamWriter(filePath);
            using var jw = new JsonTextWriter(sw);
            jw.Formatting = Formatting.Indented;
            doc.WriteTo(jw);
        }
    }
}
