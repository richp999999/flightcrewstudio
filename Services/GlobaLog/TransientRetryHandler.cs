using System.Net;

namespace FlightCrewStudio.Services.GlobaLog;

/// <summary>
/// Retries GlobaLog requests that fail before any response arrives (connection refused or reset,
/// TLS handshake dropped, DNS hiccup) and GETs that get a 502/503/504 from GlobaLog's front end.
/// Waits 2 s, 5 s and 10 s between attempts. If every attempt fails, the error is turned into a
/// <see cref="GlobaLogException"/> with a plain explanation, which the Sync page shows as-is.
/// </summary>
public sealed class TransientRetryHandler(HttpMessageHandler inner, ILogger? logger = null) : DelegatingHandler(inner)
{
    private static readonly TimeSpan[] Delays = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // Buffer any body once so it can be sent again on a retry.
        if (request.Content is not null) await request.Content.LoadIntoBufferAsync(ct);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var response = await base.SendAsync(request, ct);
                if (attempt < Delays.Length && request.Method == HttpMethod.Get
                    && response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
                {
                    logger?.LogWarning("GlobaLog returned {Status} for {Url}; retrying in {Delay}s", (int)response.StatusCode, request.RequestUri, Delays[attempt].TotalSeconds);
                    response.Dispose();
                    await Task.Delay(Delays[attempt], ct);
                    continue;
                }
                return response;
            }
            catch (HttpRequestException ex) when (!ct.IsCancellationRequested)
            {
                if (attempt >= Delays.Length)
                    throw new GlobaLogException(Explain(ex, request.RequestUri), ex);
                logger?.LogWarning("Could not reach GlobaLog for {Url} ({Error}); retrying in {Delay}s", request.RequestUri, Innermost(ex).Message, Delays[attempt].TotalSeconds);
                await Task.Delay(Delays[attempt], ct);
            }
        }
    }

    private static string Explain(HttpRequestException ex, Uri? uri)
    {
        var site = uri is null ? "GlobaLog" : $"{uri.Scheme}://{uri.Host}";
        var detail = Innermost(ex).Message;
        var tls = ex.InnerException is IOException or System.Security.Authentication.AuthenticationException;
        return tls
            ? $"Couldn't open a secure connection to {site} after {Delays.Length + 1} attempts — the connection was closed during the SSL/TLS handshake ({detail}). " +
              "This is a network problem between this server and GlobaLog rather than a login problem: check that the machine running " +
              $"FlightCrewStudio can open {site} in a browser, and whether a VPN, proxy, firewall or antivirus “web protection” is interfering. " +
              "If it only happens after several syncs, GlobaLog may be temporarily refusing connections — wait a while and try again."
            : $"Couldn't connect to {site} after {Delays.Length + 1} attempts ({detail}). Check this server's internet connection and that {site} is up.";
    }

    private static Exception Innermost(Exception ex)
    {
        while (ex.InnerException is not null) ex = ex.InnerException;
        return ex;
    }
}
