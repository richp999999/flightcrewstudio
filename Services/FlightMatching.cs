using FlightCrewStudio.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Services;

/// <summary>
/// The one rule for "is this the same flight?" when flights come from different sources (a CSV
/// import, a GlobaLog sync): the same departure airport and off-block times within
/// <see cref="Window"/> of each other. Used by the logbook.aero import (to skip flights already
/// stored) and by sync (to take over an imported flight instead of adding a second copy).
/// Within GlobaLog itself, its own flight id is what identifies a flight.
/// </summary>
public static class FlightMatching
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The departure airport as compared: its ICAO code upper-cased, or "" when there's no usable
    /// code (an airfield written as a name, e.g. "Doncaster Sheffield", is stored without an airport).
    /// </summary>
    public static string Key(string? departure)
    {
        var s = departure?.Trim().ToUpperInvariant();
        return s is { Length: >= 3 and <= 4 } && s.All(char.IsLetterOrDigit) ? s : "";
    }

    public static bool IsMatch(string? departureA, DateTime offBlockA, string? departureB, DateTime offBlockB) =>
        Key(departureA) == Key(departureB) && (offBlockA - offBlockB).Duration() <= Window;

    /// <summary>
    /// The stored flight (of <paramref name="flights"/>, for this user) that matches, closest in
    /// time first; <paramref name="accept"/> can rule candidates out. Null when there's none.
    /// </summary>
    public static async Task<Flight?> FindAsync(IQueryable<Flight> flights, string userId, string? departure, DateTime offBlock,
        Func<Flight, bool>? accept = null, CancellationToken ct = default)
    {
        var from = offBlock - Window;
        var to = offBlock + Window;
        var candidates = await flights
            .Include(f => f.DepartureAirport)
            .Where(f => f.UserId == userId && f.OffBlock >= from && f.OffBlock <= to)
            .ToListAsync(ct);
        return candidates
            .Where(f => IsMatch(f.DepartureAirport?.Icao, f.OffBlock!.Value, departure, offBlock))
            .Where(f => accept is null || accept(f))
            .OrderBy(f => (f.OffBlock!.Value - offBlock).Duration())
            .FirstOrDefault();
    }
}
