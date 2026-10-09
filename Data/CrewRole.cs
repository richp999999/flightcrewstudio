using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Data;

/// <summary>
/// A crew position (Captain, First officer, Cabin crew, …). Referenced by id from
/// <see cref="FlightCrew"/> (role held on a flight), <see cref="Flight"/> (the user's own role)
/// and <see cref="CrewMember"/> (latest role). The standard roles are seeded; any new role name
/// met during sync is added automatically.
/// </summary>
[Index(nameof(Name), IsUnique = true)]
public class CrewRole
{
    public const int CaptainId = 1, FirstOfficerId = 2, SecondOfficerId = 3, CabinCrewId = 4, PassiveId = 5;

    /// <summary>Roles that never count as flight time: positioning, cabin crew and supernumerary seats.</summary>
    public static readonly string[] NotFlightTimeNames = ["Passive", "Cabin crew", "Supernumerary"];

    /// <summary>
    /// Whether a flight on which the user held <paramref name="myRole"/> counts as flight time.
    /// No role set counts. The dashboard, the Flights page totals and the logbook exports all use
    /// this rule (database queries use <see cref="CountsAsFlightTime"/> directly).
    /// </summary>
    public static bool Counts(CrewRole? myRole) => myRole?.CountsAsFlightTime ?? true;

    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = default!;

    /// <summary>Short label for badges, e.g. "CPT", "FO".</summary>
    [MaxLength(6)]
    public string? ShortName { get; set; }

    /// <summary>Display order in lists and dropdowns.</summary>
    public int SortOrder { get; set; } = 100;

    /// <summary>Pilot positions (shown as avatars in the flight list).</summary>
    public bool IsFlightDeck { get; set; }

    /// <summary>
    /// Whether flights where this is the user's own role count towards flight time and flight
    /// counts. False for Passive (positioning), Cabin crew and Supernumerary.
    /// </summary>
    public bool CountsAsFlightTime { get; set; } = true;

    public string Short => ShortName ?? (Name.Length <= 3 ? Name.ToUpperInvariant() : Name[..3].ToUpperInvariant());

    public static readonly CrewRole[] Seed =
    [
        new() { Id = CaptainId, Name = "Captain", ShortName = "CPT", SortOrder = 10, IsFlightDeck = true },
        new() { Id = FirstOfficerId, Name = "First officer", ShortName = "FO", SortOrder = 20, IsFlightDeck = true },
        new() { Id = SecondOfficerId, Name = "Second officer", ShortName = "SO", SortOrder = 30, IsFlightDeck = true },
        new() { Id = CabinCrewId, Name = "Cabin crew", ShortName = "CC", SortOrder = 40, CountsAsFlightTime = false },
        new() { Id = PassiveId, Name = "Passive", ShortName = "PAX", SortOrder = 50, CountsAsFlightTime = false },
    ];
}
