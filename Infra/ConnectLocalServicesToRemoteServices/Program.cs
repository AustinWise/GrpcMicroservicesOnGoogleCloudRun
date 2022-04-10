using ConnectLocalServicesToRemoteServices;
using InfraLib;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text.Json;

// TODO: make these configurable or discover them automatically from Pulumi.yaml
const string API_BASE = "https://api.pulumi.com";
const string ORGANIZATION = "AustinWise";
const string PROJECT = "google-cloud-run-microservices";
const string STACK = "dev";

var repoRoot = RootFinder.GetRepoRoot();
var allServices = ServiceDefinition.GetAllServices().ToDictionary(s => s.Name);

string credsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pulumi", "credentials.json");
JsonDocument credsDoc;
using (var fs = File.OpenRead(credsPath))
{
    credsDoc = JsonDocument.Parse(fs);
}

string? accessToken = credsDoc.RootElement.GetProperty("accessTokens").GetProperty(API_BASE).GetString();
if (accessToken is null)
    throw new Exception("Failed to get access token");

var client = new HttpClient();
client.BaseAddress = new Uri(API_BASE);
client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.pulumi+8"));
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", accessToken);

var response = await client.GetAsync($"/api/stacks/{ORGANIZATION}/{PROJECT}/{STACK}/export");
var apiRes = await JsonSerializer.DeserializeAsync<Response>(await response.Content.ReadAsStreamAsync());
if (apiRes is null)
    throw new Exception("null response");

if (apiRes.Version != 3)
    throw new Exception("Unexpected version" + apiRes.Version);

if (apiRes.Deployment?.Resources is null)
    throw new Exception("Missing resources");

foreach (var res in apiRes.Deployment.Resources)
{
    if (res is null || res.Type != "gcp:cloudrun/service:Service")
        continue;

    var spec = res?.Inputs?["template"].Deserialize<ServiceTemplate>()?.Spec;
    var envs = spec?.Containers?.Single().Envs;
    var serviceName = spec?.ServiceAccountName?.Split('@')[0];

    if (envs is null || serviceName is null)
        throw new Exception("Failed to get information about service: " + res?.URN);

    var settings = new Dictionary<string, JToken>();

    foreach (var env in envs)
    {
        settings[env?.Name?.Replace("__", ":")!] = env!.Value;
    }

    allServices[serviceName].UpdateAppSettings(repoRoot, settings);
}

Console.WriteLine("Done writing app settings for connecting to hosted services.");
