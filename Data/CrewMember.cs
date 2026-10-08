using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Data;

/// <summary>
/// A person who has flown with the user, as listed in the Crew section of GlobaLog flights.
/// One record per person per app user; every flight they appear on links to it through
/// <see cref="FlightCrew"/>, which also records the role they held on that flight.
///
/// Matching: GlobaLog sometimes shows a crew member as a name ("Laura PEARCE") and sometimes
/// only as a staff number ("40016792"). Names are matched on <see cref="NormalizedName"/>
/// (case-insensitive, whitespace-collapsed); staff numbers on <see cref="StaffNumber"/>.
/// Two different people with exactly the same name will be merged into one record.
/// </summary>
[Index(nameof(UserId), nameof(NormalizedName), IsUnique = true)]
[Index(nameof(UserId), nameof(StaffNumber), IsUnique = true)]
public partial class CrewMember
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = default!;
    public ApplicationUser? User { get; set; }

    /// <summary>Display name as shown by GlobaLog (null when only a staff number is known).</summary>
    [MaxLength(200)]
    public string? Name { get; set; }

    /// <summary>Upper-cased, whitespace-normalised name used for matching.</summary>
    [MaxLength(200)]
    public string? NormalizedName { get; set; }

    /// <summary>Staff / load id, when GlobaLog lists the person by number.</summary>
    [MaxLength(50)]
    public string? StaffNumber { get; set; }

    /// <summary>The role held on the most recent flight synced (e.g. "Captain", "Cabin crew").</summary>
    public int? LatestCrewRoleId { get; set; }
    public CrewRole? LatestCrewRole { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<FlightCrew> Flights { get; set; } = new();

    public string DisplayName => Name ?? (StaffNumber is not null ? $"Staff no. {StaffNumber}" : "(unknown)");

    // ---- matching helpers -------------------------------------------------------------

    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
    [GeneratedRegex(@"\s*-\s*")] private static partial Regex Hyphen();
    [GeneratedRegex(@"^\d{4,}$")] private static partial Regex StaffNo();

    /// <summary>Tidy a name for display: trim, collapse spaces, "Amber- Lily" → "Amber-Lily".</summary>
    public static string CleanName(string raw) => Hyphen().Replace(Whitespace().Replace(raw.Trim(), " "), "-");

    public static string NormalizeName(string raw) => CleanName(raw).ToUpperInvariant();

    public static bool LooksLikeStaffNumber(string raw) => StaffNo().IsMatch(raw.Trim());

    /// <summary>Dictionary key used to find an existing member: "#&lt;staffno&gt;" or "N:&lt;NAME&gt;".</summary>
    public static string? MatchKey(string? name, string? staffNumber)
    {
        if (!string.IsNullOrWhiteSpace(staffNumber)) return "#" + staffNumber.Trim();
        if (!string.IsNullOrWhiteSpace(name)) return "N:" + NormalizeName(name);
        return null;
    }

    public string? MatchKey() => StaffNumber is not null ? "#" + StaffNumber : NormalizedName is not null ? "N:" + NormalizedName : null;
}
