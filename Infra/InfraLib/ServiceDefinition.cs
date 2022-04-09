using System.Collections.ObjectModel;

namespace InfraLib
{
    public record ServiceDefinition(string Name, string RepoRelativePath, ReadOnlyCollection<ServiceDefinition> Dependancies, bool IsPublic = false)
    {
        static ReadOnlyCollection<ServiceDefinition> CreateList(params ServiceDefinition[] defs)
        {
            return new ReadOnlyCollection<ServiceDefinition>(defs);
        }

        private static IEnumerable<ServiceDefinition> GetAllServicesCore()
        {
            var backend = new ServiceDefinition("backend-service", "BackendService", CreateList());
            yield return backend;

            yield return new ServiceDefinition("frontend-service", "FrontendWebApp", CreateList(backend), IsPublic: true);
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

        private static void ValidateLackOfCycles(Stack<ServiceDefinition> seen, ServiceDefinition node)
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
    }
}