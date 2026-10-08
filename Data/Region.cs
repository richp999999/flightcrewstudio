using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Data;

/// <summary>
/// A region within a country (ISO 3166-2 style code, e.g. "GB-ENG"). Shared reference data
/// managed by administrators (Admin → Regions), importable from the OurAirports regions.csv.
/// </summary>
[Index(nameof(Code), IsUnique = true)]
public class Region
{
    public int Id { get; set; }

    [Required, MaxLength(10)]
    public string Code { get; set; } = default!;

    /// <summary>Code within the country, e.g. "ENG".</summary>
    [MaxLength(10)]
    public string? LocalCode { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = default!;

    [MaxLength(2)]
    public string? Continent { get; set; }

    public int? CountryId { get; set; }
    public Country? Country { get; set; }

    [MaxLength(255)]
    public string? WikipediaLink { get; set; }
}
