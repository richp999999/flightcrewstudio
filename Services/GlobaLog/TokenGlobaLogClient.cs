using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FlightCrewStudio.Services.GlobaLog;

/// <summary>
/// Talks to the documented GlobaLog API using an operator-issued token in the X-Token header.
/// This is the robust path — it uses the public /api/v1 endpoints directly.
/// </summary>
public sealed class TokenGlobaLogClient : IGlobaLogClient
{
    private readonly HttpClient _http;
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public TokenGlobaLogClient(string baseUrl, string apiToken, int timeoutSeconds)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        _http.DefaultRequestHeaders.Add("X-Token", apiToken);
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public async Task<GlobaLogHello> TestConnectionAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync("/api/v1/hello", ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
            throw new GlobaLogException("GlobaLog rejected the API token (401 Unauthorized). Check the token is correct and active.");
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<GlobaLogHello>(Json, ct))
               ?? throw new GlobaLogException("Unexpected empty response from GlobaLog /hello.");
    }

    public async Task<GlobaLogFlightPage> GetFlightsPageAsync(int page, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync($"/api/v1/flights?page={page}", ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
            throw new GlobaLogException("GlobaLog rejected the API token (401 Unauthorized) while fetching flights.");
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<GlobaLogFlightPage>(Json, ct))
               ?? new GlobaLogFlightPage();
    }

    /// <summary>The API includes crew (staff number + role) and airports in the flight itself.</summary>
    public Task<GlobaLogFlightDetails?> GetFlightDetailsAsync(GlobaLogFlight flight, CancellationToken ct = default) =>
        Task.FromResult<GlobaLogFlightDetails?>(new GlobaLogFlightDetails
        {
            Crew = flight.Crew,
            Departure = flight.Departure?.Airport,
            Arrival = flight.Arrival?.Airport
        });

    public void Dispose() => _http.Dispose();
}
