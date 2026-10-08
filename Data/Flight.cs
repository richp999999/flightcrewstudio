using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Data;

/// <summary>
/// A flight downloaded from GlobaLog and stored locally. Deduplication is by the pair
/// (<see cref="UserId"/>, <see cref="GlobaLogFlightId"/>): a flight already present is updated
/// in place rather than inserted again. A unique index enforces this at the database level.
/// </summary>
[Index(nameof(UserId), nameof(GlobaLogFlightId), IsUnique = true)]
public class Flight
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = default!;
    public ApplicationUser? User { get; set; }

    /// <summary>The flight's "id" in GlobaLog — the stable natural key used for matching/dedup.</summary>
    public long GlobaLogFlightId { get; set; }

    public string? Name { get; set; }          // callsign, e.g. "RND1234"
    public string? Status { get; set; }         // e.g. "COMMITED"

    public DateTime? OffBlock { get; set; }
    public DateTime? DepartureTime { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public DateTime? OnBlock { get; set; }

    public double? RadiationAmount { get; set; }
    public int TotalDayMinutes { get; set; }
    public int TotalNightMinutes { get; set; }
    public int TotalInstrMinutes { get; set; }
    public bool NightLanding { get; set; }
    public string? ForeignId { get; set; }

    /// <summary>
    /// The user's own role on this flight. Taken from the flight's crew list when the user's
    /// crew member is set in Settings; otherwise from the logbook export's code ("CPT" → Captain).
    /// </summary>
    public int? MyCrewRoleId { get; set; }
    public CrewRole? MyCrewRole { get; set; }

    /// <summary>Whether the user was pilot flying (logbook export "flying_pilot").</summary>
    public bool? PilotFlying { get; set; }

    // Route
    public int? DepartureAirportId { get; set; }
    public Airport? DepartureAirport { get; set; }
    public int? ArrivalAirportId { get; set; }
    public Airport? ArrivalAirport { get; set; }

    // Aircraft
    public int? AircraftId { get; set; }
    public Aircraft? Aircraft { get; set; }

    public List<FlightCrew> Crew { get; set; } = new();

    /// <summary>Free-text remarks (entered in this app).</summary>
    [MaxLength(1000)]
    public string? Remarks { get; set; }

    /// <summary>
    /// True once the user has edited this flight here. Sync then leaves the flight (and its crew)
    /// untouched so local corrections aren't overwritten by GlobaLog's data.
    /// </summary>
    public bool EditedLocally { get; set; }
    public DateTime? EditedAt { get; set; }

    /// <summary>Source timestamps from GlobaLog.</summary>
    public DateTime? SourceCreatedAt { get; set; }
    public DateTime? SourceUpdatedAt { get; set; }

    /// <summary>When this app first stored the flight and last refreshed it.</summary>
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Links a <see cref="Flight"/> to a <see cref="CrewMember"/>, with the role they held on that
/// flight ("Captain", "First officer", "Cabin crew", "Passive", …).
/// </summary>
[Index(nameof(FlightId), nameof(CrewMemberId))]
public class FlightCrew
{
    public int Id { get; set; }

    public int FlightId { get; set; }
    public Flight? Flight { get; set; }

    public int CrewMemberId { get; set; }
    public CrewMember? CrewMember { get; set; }

    /// <summary>Role held on this flight.</summary>
    public int? CrewRoleId { get; set; }
    public CrewRole? CrewRole { get; set; }

    /// <summary>Order within the flight's crew list as shown by GlobaLog.</summary>
    public int Position { get; set; }
}
