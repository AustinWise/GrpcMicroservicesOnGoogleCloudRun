using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConnectLocalServicesToRemoteServices
{
    internal class Response
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("deployment")]
        public Deployment? Deployment { get; set; }
    }

    internal class Deployment
    {
        [JsonPropertyName("resources")]
        public List<Resource>? Resources { get; set; }
    }

    internal class Resource
    {
        [JsonPropertyName("urn")]
        public string? URN { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("inputs")]
        public Dictionary<string, JsonElement>? Inputs { get; set; }

        [JsonPropertyName("outputs")]
        public Dictionary<string, JsonElement>? Outputs { get; set; }
    }


    internal class Env
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }

    internal class ServiceTemplate
    {
        [JsonPropertyName("spec")]
        public ServiceSpec? Spec { get; set; }
    }

    internal class ServiceSpec
    {
        [JsonPropertyName("containers")]
        public List<ServiceContainer>? Containers { get; set; }

        [JsonPropertyName("serviceAccountName")]
        public string? ServiceAccountName { get; set; }
    }

    internal class ServiceContainer
    {
        [JsonPropertyName("envs")]
        public List<Env>? Envs { get; set; }
    }

    internal class ServiceStatus
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
