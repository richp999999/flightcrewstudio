using FlightCrewStudio.Data;

namespace FlightCrewStudio.Services.DayNight;

/// <summary>Applies <see cref="DayNightCalculator"/> to a stored flight.</summary>
public static class FlightDayNight
{
    /// <summary>
    /// Calculate day/night minutes for a flight from its off-block/on-block times and its departure
    /// and arrival airports' positions, and whether it was night at the destination when the aircraft
    /// landed (at <paramref name="landing"/>, or on-block if the landing time isn't known).
    /// Returns null (and explains why) when it can't be calculated.
    /// </summary>
    public static DayNightResult? Calculate(DateTime? offBlock, DateTime? onBlock, Airport? dep, Airport? arr, out string? reason,
        DateTime? landing = null)
    {
        reason = null;
        if (offBlock is null || onBlock is null) { reason = "Off-block and on-block times are needed."; return null; }
        if (onBlock <= offBlock) { reason = "On-block is not after off-block."; return null; }
        if (dep?.Latitude is not { } dLat || dep.Longitude is not { } dLon) { reason = $"No position for the departure airport{(dep is null ? "" : $" ({dep.Icao})")} — import airport data or add it under Admin → Airports."; return null; }
        if (arr?.Latitude is not { } aLat || arr.Longitude is not { } aLon) { reason = $"No position for the arrival airport{(arr is null ? "" : $" ({arr.Icao})")} — import airport data or add it under Admin → Airports."; return null; }

        var r = DayNightCalculator.Calculate(
            DateTime.SpecifyKind(offBlock.Value, DateTimeKind.Utc), DateTime.SpecifyKind(onBlock.Value, DateTimeKind.Utc),
            dLat, dLon, aLat, aLon);
        if (r is null) { reason = "The block time is implausible (over 24 hours)."; return null; }

        // Night landing: the sun's position at the destination at touchdown, using the same
        // definition of night as the day/night split above.
        var touchdown = landing is { } l && l >= offBlock && l <= onBlock ? l : onBlock.Value;
        return r.Value with { NightLanding = DayNightCalculator.IsNight(DateTime.SpecifyKind(touchdown, DateTimeKind.Utc), aLat, aLon) };
    }

    /// <summary>Recalculate and store day/night time and the night-landing flag. Returns true if the values were set.</summary>
    public static bool Apply(Flight f, Airport? dep, Airport? arr)
    {
        var r = Calculate(f.OffBlock, f.OnBlock, dep, arr, out _, f.ArrivalTime);
        if (r is null) return false;
        f.TotalDayMinutes = r.Value.DayMinutes;
        f.TotalNightMinutes = r.Value.NightMinutes;
        f.NightLanding = r.Value.NightLanding;
        return true;
    }
}
