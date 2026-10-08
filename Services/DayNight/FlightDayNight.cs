using FlightCrewStudio.Data;

namespace FlightCrewStudio.Services.DayNight;

/// <summary>Applies <see cref="DayNightCalculator"/> to a stored flight.</summary>
public static class FlightDayNight
{
    /// <summary>
    /// Calculate day/night minutes for a flight from its off-block/on-block times and its departure
    /// and arrival airports' positions. Returns null (and explains why) when it can't be calculated.
    /// </summary>
    public static DayNightResult? Calculate(DateTime? offBlock, DateTime? onBlock, Airport? dep, Airport? arr, out string? reason)
    {
        reason = null;
        if (offBlock is null || onBlock is null) { reason = "Off-block and on-block times are needed."; return null; }
        if (onBlock <= offBlock) { reason = "On-block is not after off-block."; return null; }
        if (dep?.Latitude is not { } dLat || dep.Longitude is not { } dLon) { reason = $"No position for the departure airport{(dep is null ? "" : $" ({dep.Icao})")} — import airport data or add it under Admin → Airports."; return null; }
        if (arr?.Latitude is not { } aLat || arr.Longitude is not { } aLon) { reason = $"No position for the arrival airport{(arr is null ? "" : $" ({arr.Icao})")} — import airport data or add it under Admin → Airports."; return null; }

        var r = DayNightCalculator.Calculate(
            DateTime.SpecifyKind(offBlock.Value, DateTimeKind.Utc), DateTime.SpecifyKind(onBlock.Value, DateTimeKind.Utc),
            dLat, dLon, aLat, aLon);
        if (r is null) reason = "The block time is implausible (over 24 hours).";
        return r;
    }

    /// <summary>Recalculate and store day/night on the flight. Returns true if the values were set.</summary>
    public static bool Apply(Flight f, Airport? dep, Airport? arr)
    {
        var r = Calculate(f.OffBlock, f.OnBlock, dep, arr, out _);
        if (r is null) return false;
        f.TotalDayMinutes = r.Value.DayMinutes;
        f.TotalNightMinutes = r.Value.NightMinutes;
        return true;
    }
}
