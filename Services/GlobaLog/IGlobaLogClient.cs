namespace FlightCrewStudio.Services.GlobaLog;

/// <summary>
/// Reads flight data from GlobaLog. Implementations differ only in how they authenticate
/// (web-session cookie vs. X-Token header); the rest of the app talks to this interface.
/// </summary>
public interface IGlobaLogClient : IDisposable
{
    /// <summary>Verify the credentials work and return the greeting (name + operator).</summary>
    Task<GlobaLogHello> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>Fetch one page (1-based) of flights, ordered by off-block descending (100/page).</summary>
    Task<GlobaLogFlightPage> GetFlightsPageAsync(int page, CancellationToken ct = default);

    /// <summary>
    /// Return the crew list (and fuller airport details, where available) for a flight from
    /// <see cref="GetFlightsPageAsync"/>, or null when it can't be determined (existing crew links
    /// are then left untouched).
    /// </summary>
    Task<GlobaLogFlightDetails?> GetFlightDetailsAsync(GlobaLogFlight flight, CancellationToken ct = default);
}

/// <summary>Raised when GlobaLog authentication or fetching fails, with a user-friendly message.</summary>
public class GlobaLogException : Exception
{
    public GlobaLogException(string message, Exception? inner = null) : base(message, inner) { }
}
