
This repo shows how you can do the following:

* Create a GRPC service using ASP.NET Core
* Run it in Google Cloud Run
* Create a frontend ASP.NET Core web app and run it in Google Cloud Run as well
* Authenticate using s service account when calling the GRPC service from the frontend.

It is designed to be run in one of three modes

* Production. All services run in Google Cloud Run and authenticate their communication
  with each other using service accounts.
* Testing locally. All services run local and communicate directly with each other.
* Testing a local service against a service running in Google Cloud Run. This will
  use the developer's credentials to impersonate the appropriate service account.

# How to run

## Production

This uses [Pulumi](https://www.pulumi.com/) to deploy.

```bash
pulumi up
```

## Testing locally

This will configure each service to listen on a unqiue port and configure them
to connect to each other on localhost.

```bash
dotnet run --project Infra/GenerateAppsettings/GenerateAppsettings.csproj
```

## Testing locally against remote services

This will configure every service to connect to Google Cloud Run for the services
it depends on. It relies on the Pulumi API to discovery the addresses of deployed
services.

```bash
pulumi up
dotnet run --project Infra/ConnectLocalServicesToRemoteServices/ConnectLocalServicesToRemoteServices.csproj
```

Your GCP account will need
[permission to impersonate service account](https://cloud.google.com/iam/docs/impersonating-service-accounts)
for this to work.

# How it works

There is an
[abstract definition](Infra/InfraLib/ServiceDefinition.cs)
of all the services and how they depend on each other. Each service has an
associated `Dockerfile` to describe the Docker image that should be deployed to
Google Cloud Run.
[A Pulumi program](Infra/PulumiInfra/MyStack.cs)
translate the abstract definition of services into Google Could Run services
and uses IAM polices to connect frontend services to backend services.

The same abstract definition is also used to generate configurations that
[run entirely locally](Infra/GenerateAppsettings/Program.cs)
and
[connect a local service to a service running Cloud Run](Infra/ConnectLocalServicesToRemoteServices/Program.cs).

When a service attempts to connect to remote service, it will try to figure out
if it needs a credential based on the URL of the remote service. If needed, a
credential for the appropriate service account [will be used](GrpcContracts/GoogleAuthConfigurationExtensions.cs).

# Alternatives to this approach

[Tye](https://github.com/dotnet/tye) is a similar tool that has a lot more
polish. There are a could things about the approach used in this Repo compared
to Tye:

* This setup uses a "serverless" system to run the services (specifically Google Cloud Run, which is based on [Knative](https://knative.dev/docs/) ).
  This means this means the system will automatically scale the resource up and
  down as needed, including potentially all the way to zero. This might have cost
  and other advantages over using Tye, which deploys to a Kubernetes cluster.
* [Tye's Ingress System](https://github.com/dotnet/tye/blob/main/docs/recipes/ingress.md)
  allows flexible routing of requests at layer 7, i.e. based on HTTP request paths.
  This repo just has a `IsPublic` boolean setting for each service, which is less
  flexible.
* Tye allows every service to talk to every other service. This might not be desired
  from a "least privilege" security perspective and from an architectural
  layering perspective. The system in this repo requires services be specifically
  allowed to talk to each other. Also cycles are not allowed in the dependency
  graph. This makes it harder to construct services that can only start if they
  are already running.

# TODO

* Make example services more interesting and have more of them.
* Add a `graphviz` generation option to show how services connect to each other.
* Add support for generating a Docker Compose config based on the abstract definition.
* Figure out how access to a datastore would fit into this system. For a traditional
  datastore it could leverage the Docker Compose to run things like Redis and Postgresql
  locally. I'm not sure what it would do for Firebase. Run the emulator locally?
* Package all these random projects into a coherent tool.
