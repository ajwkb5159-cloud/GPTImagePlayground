using System.Net.Http.Headers;
using System.Net.Security;

namespace ImageGenerator.Services;

/// <summary>
/// Shared HTTP transport setup for API calls. The two handlers are pooled static instances so
/// connections and TLS settings are reused between image requests and model-list lookups.
/// </summary>
internal static class ApiHttpClientFactory
{
    private static readonly SocketsHttpHandler VerifiedHttpHandler = CreateHttpHandler(true);
    private static readonly SocketsHttpHandler UnverifiedHttpHandler = CreateHttpHandler(false);

    public static HttpClient Create(string apiKey, bool verifySslCertificate, int timeoutMinutes)
    {
        var handler = verifySslCertificate ? VerifiedHttpHandler : UnverifiedHttpHandler;
        var httpClient = new HttpClient(handler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromMinutes(Math.Max(1, timeoutMinutes)),
        };
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        return httpClient;
    }

    private static SocketsHttpHandler CreateHttpHandler(bool verifySslCertificate)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        if (!verifySslCertificate)
        {
            handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
            };
        }

        return handler;
    }
}
