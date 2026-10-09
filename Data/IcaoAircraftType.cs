using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Data;

/// <summary>
/// An ICAO aircraft type designator (Doc 8643), e.g. "A21N" — AIRBUS A-321neo, L2J, medium wake.
/// Shared reference data managed by administrators (Admin → Aircraft Types) and importable from
/// icao_aircraft_types.csv; linked to its <see cref="Manufacturer"/>. Each <see cref="Aircraft"/>
/// links to one, and a flight's type is the type of the aircraft it was flown on.
/// </summary>
[Index(nameof(Designator), IsUnique = true)]
public class IcaoAircraftType
{
    public int Id { get; set; }

    /// <summary>ICAO type designator, 2–4 characters, e.g. "A21N", "B752".</summary>
    [Required, MaxLength(4)]
    public string Designator { get; set; } = default!;

    public int? ManufacturerId { get; set; }
    public Manufacturer? Manufacturer { get; set; }

    /// <summary>Model name, e.g. "A-321neo".</summary>
    [Required, MaxLength(100)]
    public string Model { get; set; } = default!;

    /// <summary>ICAO description: aircraft kind, engine count, engine type — e.g. "L2J".</summary>
    [MaxLength(3)]
    public string? Description { get; set; }

    /// <summary>LandPlane, SeaPlane, Amphibian, Helicopter, Gyrocopter, Tiltrotor.</summary>
    [MaxLength(20)]
    public string? AircraftKind { get; set; }

    /// <summary>"1"–"8", or "C" (coupled).</summary>
    [MaxLength(2)]
    public string? EngineCount { get; set; }

    /// <summary>Jet, Turboprop/Turboshaft, Piston, Electric, Rocket.</summary>
    [MaxLength(30)]
    public string? EngineType { get; set; }

    /// <summary>Wake turbulence category: L, M, H, J (or L/M).</summary>
    [MaxLength(3)]
    public string? Wtc { get; set; }

    /// <summary>RECAT-EU wake turbulence group (A–G, Z).</summary>
    [MaxLength(1)]
    public string? Wtg { get; set; }

    public static string? NormalizeDesignator(string? d) =>
        string.IsNullOrWhiteSpace(d) ? null : d.Trim().ToUpperInvariant();

    // Ordered: the more specific patterns come first. Matched against the name upper-cased with
    // spaces and hyphens removed, e.g. "A-321neo" → "A321NEO", "757-200" → "757200".
    private static readonly (System.Text.RegularExpressions.Regex Pattern, string Code)[] Known =
    [
        (new(@"^A?318"), "A318"),
        (new(@"^A?319NEO"), "A19N"), (new(@"^A?319"), "A319"),
        (new(@"^A?320NEO"), "A20N"), (new(@"^A?320"), "A320"),
        (new(@"^A?321NEO"), "A21N"), (new(@"^A?321"), "A321"),
        (new(@"^A?330200"), "A332"), (new(@"^A?330300"), "A333"), (new(@"^A?330900"), "A339"),
        (new(@"^A?3501000"), "A35K"), (new(@"^A?350"), "A359"),
        (new(@"^A?380"), "A388"),
        (new(@"^B?737MAX8|^B?7378$"), "B38M"), (new(@"^B?737700"), "B737"), (new(@"^B?737800"), "B738"), (new(@"^B?737900"), "B739"),
        (new(@"^B?757200"), "B752"), (new(@"^B?757300"), "B753"),
        (new(@"^B?767300"), "B763"), (new(@"^B?777300ER"), "B77W"), (new(@"^B?777200"), "B772"),
        (new(@"^B?7878"), "B788"), (new(@"^B?7879"), "B789"), (new(@"^B?78710"), "B78X"),
    ];

    /// <summary>"A-321neo" → "A21N", "757-200" → "B752", "A21N" → "A21N"; null when unknown.</summary>
    public static string? GuessIcaoCode(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var key = name.ToUpperInvariant().Replace("-", "").Replace(" ", "");
        foreach (var (pattern, code) in Known)
            if (pattern.IsMatch(key)) return code;
        // Already a designator? (letter + 2–3 letters/digits, e.g. "A21N", "E190", "DH8D")
        return System.Text.RegularExpressions.Regex.IsMatch(key, @"^[A-Z][A-Z0-9]{2,3}$") ? key : null;
    }

    public static readonly string[] AircraftKinds = ["LandPlane", "SeaPlane", "Amphibian", "Helicopter", "Gyrocopter", "Tiltrotor"];
    public static readonly string[] EngineTypes = ["Jet", "Turboprop/Turboshaft", "Piston", "Electric", "Rocket"];
    public static readonly string[] WakeCategories = ["L", "M", "H", "J", "L/M"];
}
