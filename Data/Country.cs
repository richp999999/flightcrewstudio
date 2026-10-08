using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Data;

/// <summary>
/// A country (ISO 3166-1 alpha-2 code, e.g. "GB"). Shared reference data managed by
/// administrators (Admin → Countries), importable from the OurAirports countries.csv.
/// </summary>
[Index(nameof(Code), IsUnique = true)]
public class Country
{
    public int Id { get; set; }

    [Required, MaxLength(2)]
    public string Code { get; set; } = default!;

    [Required, MaxLength(100)]
    public string Name { get; set; } = default!;

    /// <summary>Continent code: AF, AN, AS, EU, NA, OC, SA.</summary>
    [MaxLength(2)]
    public string? Continent { get; set; }

    [MaxLength(255)]
    public string? WikipediaLink { get; set; }

    public List<Region> Regions { get; set; } = new();
}
