using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Data;

/// <summary>
/// An aircraft manufacturer as listed by ICAO (Doc 8643), identified by its code (e.g. "AIRBUS",
/// "BOEING"). Shared reference data managed by administrators (Admin → Manufacturers) and
/// importable from icao_manufacturers.csv. <see cref="IcaoAircraftType"/> rows link to it.
/// </summary>
[Index(nameof(Code), IsUnique = true)]
public class Manufacturer
{
    public int Id { get; set; }

    /// <summary>ICAO manufacturer code, e.g. "AIRBUS", "328 SUPPORT SERVICES".</summary>
    [Required, MaxLength(100)]
    public string Code { get; set; } = default!;

    /// <summary>Full name, e.g. "Airbus SAS (France/Germany/UK/Spain)".</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = default!;

    /// <summary>State (country), when ICAO gives one.</summary>
    [MaxLength(100)]
    public string? State { get; set; }

    public List<IcaoAircraftType> AircraftTypes { get; set; } = new();

    public static string? NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
}
