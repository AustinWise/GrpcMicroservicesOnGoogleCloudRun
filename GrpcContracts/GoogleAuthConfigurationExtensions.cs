﻿using Google.Apis.Auth.OAuth2;
using Grpc.Core;
using System.Net;

namespace Microsoft.Extensions.DependencyInjection;

public static class GoogleAuthConfigurationExtensions
{
    //TODO: is the best way to detect if we need to use a token?
    //What if someone is using a local proxy to forward to the remote service?
    static bool ShouldConfigureAuthentication(Uri serviceUri)
    {
        switch (serviceUri.HostNameType)
        {
            //TODO: IPv4 loopback is actually the whole 127.0.0.0/8 space
            case UriHostNameType.IPv4:
                return !IPAddress.Parse(serviceUri.Host).Equals(IPAddress.Loopback);
            case UriHostNameType.IPv6:
                return !IPAddress.Parse(serviceUri.Host).Equals(IPAddress.IPv6Loopback);
            default:
                return !serviceUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static async Task AddGrpcClientWithGcpServiceIdentityAsync<TClient>(this IServiceCollection services, string? serviceUrl, string? serviceIdenity)
        where TClient : class
    {
        ArgumentNullException.ThrowIfNull(serviceUrl);

        var greeterUri = new Uri(serviceUrl);
        var grpc = services.AddGrpcClient<TClient>(o =>
        {
            o.Address = greeterUri;
        });

        if (ShouldConfigureAuthentication(greeterUri))
        {
            var cred = await GoogleCredential.GetApplicationDefaultAsync();
            if (cred.UnderlyingCredential is not IOidcTokenProvider)
            {
                // If we are running on our developer machine, impersonate the service identity.
                // See this article for the permissions your account needs to impersonate service identities:
                // https://cloud.google.com/iam/docs/impersonating-service-accounts
                cred = cred.Impersonate(new ImpersonatedCredential.Initializer(serviceIdenity));
            }
            var token = await cred.GetOidcTokenAsync(OidcTokenOptions.FromTargetAudience(serviceUrl));

            grpc.ConfigureChannel(o =>
            {
                var credentials = CallCredentials.FromInterceptor(async (context, metadata) =>
                {
                    // GetAccessTokenAsync() basically returns the body of this URL:
                    //   http://metadata.google.internal/computeMetadata/v1/instance/service-accounts/default/identity?audience={context.ServiceUrl}
                    // GetAccessTokenAsync() does some caching so we are not constantly downloading the above URL.
                    string tokenValue = await token.GetAccessTokenAsync();
                    metadata.Add("Authorization", $"bearer {tokenValue}");
                });

                o.Credentials = ChannelCredentials.Create(new SslCredentials(), credentials);
            });
        }
    }
}
