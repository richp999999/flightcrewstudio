using FlightCrewStudio.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Services;

/// <summary>
/// Finds a flight's ICAO aircraft type (Admin → Aircraft Types) during an import or sync.
/// In order: the ICAO designator when the source gives one (logbook.aero "A21N"); otherwise the
/// manufacturer code and model exactly as ICAO lists them (GlobaLog sends "AIRBUS" / "A-321neo",
/// the same names ICAO uses); otherwise the designator worked out from the model name
/// ("A-321-200" → A321). Gives null when the type isn't in the list. One instance per unit of work.
/// </summary>
public sealed class IcaoTypeLookup
{
    private readonly Dictionary<string, int> _byDesignator;
    private readonly Dictionary<string, int> _byMakerModel;

    private IcaoTypeLookup(List<(int Id, string Designator, string? Maker, string Model)> types)
    {
        _byDesignator = types.ToDictionary(t => t.Designator, t => t.Id, StringComparer.OrdinalIgnoreCase);
        _byMakerModel = types.Where(t => t.Maker is not null)
            .GroupBy(t => Key(t.Maker, t.Model)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
    }

    public static async Task<IcaoTypeLookup> LoadAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var rows = await db.IcaoAircraftTypes.AsNoTracking()
            .Select(t => new { t.Id, t.Designator, Maker = t.Manufacturer != null ? t.Manufacturer.Code : null, t.Model })
            .ToListAsync(ct);
        return new(rows.Select(r => (r.Id, r.Designator, r.Maker, r.Model)).ToList());
    }

    public bool IsEmpty => _byDesignator.Count == 0;

    /// <summary>The matching ICAO type's id, or null.</summary>
    public int? Find(string? designator, string? manufacturer, string? model)
    {
        if (IcaoAircraftType.NormalizeDesignator(designator) is { } d && _byDesignator.TryGetValue(d, out var id)) return id;
        if (Key(manufacturer, model) is { } k && _byMakerModel.TryGetValue(k, out id)) return id;
        if (IcaoAircraftType.GuessIcaoCode(model) is { } g && _byDesignator.TryGetValue(g, out id)) return id;
        return null;
    }

    private static string? Key(string? maker, string? model) =>
        string.IsNullOrWhiteSpace(maker) || string.IsNullOrWhiteSpace(model) ? null : $"{maker.Trim().ToUpperInvariant()}|{model.Trim()}";
}
