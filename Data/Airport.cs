using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Data;

/// <summary>
/// An airport, identified by its ICAO / OurAirports "ident" code (e.g. "EGBB"). Flights link to it
/// as departure and arrival. Shared reference data for all users, managed by administrators
/// (Admin → Airports) and importable from the OurAirports airports.csv. Sync adds airports it meets
/// and fills in details only where they're still empty, so edits and imports are kept.
/// </summary>
[Index(nameof(Icao), IsUnique = true)]
[Index(nameof(Iata))]
public class Airport
{
    public int Id { get; set; }

    /// <summary>ICAO code, or the OurAirports ident for airfields without one (up to 7 chars).</summary>
    [Required, MaxLength(10)]
    public string Icao { get; set; } = default!;

    [MaxLength(3)]
    public string? Iata { get; set; }

    /// <summary>Airport name, e.g. "Birmingham International Airport".</summary>
    [MaxLength(150)]
    public string? Name { get; set; }

    /// <summary>Town/city served, e.g. "Birmingham".</summary>
    [MaxLength(100)]
    public string? Municipality { get; set; }

    /// <summary>OurAirports type: large_airport, medium_airport, small_airport, heliport, …</summary>
    [MaxLength(20)]
    public string? Type { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? ElevationFt { get; set; }

    public int? CountryId { get; set; }
    public Country? Country { get; set; }

    public int? RegionId { get; set; }
    public Region? Region { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Short place label for lists: the town if known, otherwise the airport name.</summary>
    public string? Place => Municipality ?? Name;

    /// <summary>"EGBB — Birmingham International Airport (BHX)".</summary>
    public string DisplayName =>
        Icao + ((Name ?? Municipality) is { } n ? $" — {n}" : "") + (Iata is null ? "" : $" ({Iata})");

    public static string? NormalizeIcao(string? icao) =>
        string.IsNullOrWhiteSpace(icao) ? null : icao.Trim().ToUpperInvariant();

    /// <summary>Readable type, e.g. "large_airport" → "Large airport".</summary>
    public static string? TypeLabel(string? type) =>
        string.IsNullOrEmpty(type) ? null : char.ToUpperInvariant(type[0]) + type[1..].Replace('_', ' ');
}
