using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Data;

/// <summary>
/// An aircraft the user has flown, identified by registration. Flights link to it by
/// <see cref="Flight.AircraftId"/>. Registrations are stored without the hyphen, upper-case
/// (e.g. "GHLYH"); <see cref="DisplayRegistration"/> adds the hyphen for display ("G-HLYH").
/// Sync adds new registrations automatically and sets the type when the aircraft has none.
/// </summary>
[Index(nameof(UserId), nameof(Registration), IsUnique = true)]
public class Aircraft
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = default!;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(20)]
    public string Registration { get; set; } = default!;

    /// <summary>The aircraft's type (which also holds the manufacturer).</summary>
    public int? AircraftTypeId { get; set; }
    public AircraftType? AircraftType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string DisplayRegistration => FlightCrewStudio.Components.Shared.Fmt.Registration(Registration);

    /// <summary>"GHLYH", "G-HLYH", " g-hlyh " → "GHLYH".</summary>
    public static string? Normalize(string? reg) =>
        string.IsNullOrWhiteSpace(reg) ? null : reg.Trim().Replace("-", "").Replace(" ", "").ToUpperInvariant();
}
