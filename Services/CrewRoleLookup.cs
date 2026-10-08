using FlightCrewStudio.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Services;

/// <summary>
/// Resolves role names (from GlobaLog flight pages or the logbook export's codes) to
/// <see cref="CrewRole"/> rows, adding a new role when a name hasn't been seen before.
/// One instance per DbContext / unit of work.
/// </summary>
public sealed class CrewRoleLookup
{
    private readonly ApplicationDbContext _db;
    private readonly Dictionary<string, CrewRole> _byName;

    private CrewRoleLookup(ApplicationDbContext db, IEnumerable<CrewRole> roles)
    {
        _db = db;
        _byName = roles.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);
    }

    public static async Task<CrewRoleLookup> LoadAsync(ApplicationDbContext db, CancellationToken ct = default) =>
        new(db, await db.CrewRoles.ToListAsync(ct));

    /// <summary>Logbook export codes → role names ("CPT" → "Captain", "PSV" → "Passive", …).</summary>
    public static string? FromLogbookCode(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        null or "" => null,
        "CPT" or "CAPT" or "PIC" => "Captain",
        "FO" or "COP" or "SIC" => "First officer",
        "SO" => "Second officer",
        "CC" or "CA" => "Cabin crew",
        "PSV" or "PAX" or "DH" => "Passive",
        _ => code.Trim()
    };

    /// <summary>Find a role by name, creating it if new. Null/blank names give null.</summary>
    public CrewRole? Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        name = name.Trim();
        if (_byName.TryGetValue(name, out var role)) return role;

        role = new CrewRole { Name = name.Length > 50 ? name[..50] : name, SortOrder = 100 };
        _db.CrewRoles.Add(role);
        _byName[name] = role;
        return role;
    }
}
