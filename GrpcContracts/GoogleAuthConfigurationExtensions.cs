using Google.Apis.Auth.OAuth2;
using Grpc.Core;

namespace Microsoft.Extensions.DependencyInjection;

public static class GoogleAuthConfigurationExtensions
{
    public static IHttpClientBuilder ConfigureComputeCredentialAuth(this IHttpClientBuilder builder)
    {
        return builder.ConfigureChannel(o =>
        {
            var credentials = CallCredentials.FromInterceptor(async (context, metadata) =>
            {
                var cred = new ComputeCredential();
                var token = await cred.GetOidcTokenAsync(OidcTokenOptions.FromTargetAudience(context.ServiceUrl));
                // GetAccessTokenAsync() basically returns the body of this URL:
                //   http://metadata.google.internal/computeMetadata/v1/instance/service-accounts/default/identity?audience={context.ServiceUrl}
                // GetAccessTokenAsync() does some caching so we are not constantly downloading the above URL.
                string tokenValue = await token.GetAccessTokenAsync();
                metadata.Add("Authorization", $"bearer {tokenValue}");
            });

            o.Credentials = ChannelCredentials.Create(new SslCredentials(), credentials);
        });
    }


    public static IHttpClientBuilder ConfigureBearerAuth(this IHttpClientBuilder builder, string token)
    {
        return builder.ConfigureChannel(o =>
        {
            var credentials = CallCredentials.FromInterceptor((context, metadata) =>
            {
                metadata.Add("Authorization", $"Bearer {token}");
                return Task.CompletedTask;
            });

            o.Credentials = ChannelCredentials.Create(new SslCredentials(), credentials);
        });
    }
}
