using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Data;

/// <summary>
/// An aircraft type (e.g. "A-321" by "AIRBUS"). <see cref="Aircraft"/> rows link to it by
/// <see cref="Aircraft.AircraftTypeId"/>. One set of types per user; sync adds new types it meets.
/// </summary>
[Index(nameof(UserId), nameof(Name), IsUnique = true)]
public class AircraftType
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = default!;
    public ApplicationUser? User { get; set; }

    /// <summary>Type/model, e.g. "A-321".</summary>
    [Required, MaxLength(50)]
    public string Name { get; set; } = default!;

    /// <summary>Manufacturer, e.g. "AIRBUS".</summary>
    [MaxLength(50)]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// ICAO type designator, e.g. "A21N", "B752". Used as the "Model" in logbook exports. When
    /// blank, <see cref="Designator"/> guesses it from <see cref="Name"/>.
    /// </summary>
    [MaxLength(4)]
    public string? IcaoCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The ICAO designator if set, otherwise a guess from the name, otherwise the name.</summary>
    public string Designator => IcaoCode ?? GuessIcaoCode(Name) ?? Name;

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

    public string DisplayName => Manufacturer is null ? Name : $"{Name} ({Manufacturer})";
}
