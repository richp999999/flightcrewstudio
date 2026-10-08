using FlightCrewStudio.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Services;

/// <summary>
/// Resolves registrations to the user's <see cref="Aircraft"/> rows during a sync, adding new
/// aircraft and types as they're met. Edits made in the app win: an aircraft that already has a
/// type keeps it, and a type that already has a manufacturer keeps it. One per unit of work.
/// </summary>
public sealed class AircraftLookup
{
    private readonly ApplicationDbContext _db;
    private readonly string _userId;
    private readonly Dictionary<string, Aircraft> _byReg;
    private readonly Dictionary<string, AircraftType> _typeByName;

    private AircraftLookup(ApplicationDbContext db, string userId, IEnumerable<Aircraft> aircraft, IEnumerable<AircraftType> types)
    {
        _db = db;
        _userId = userId;
        _byReg = aircraft.ToDictionary(a => a.Registration, StringComparer.OrdinalIgnoreCase);
        _typeByName = types.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    public static async Task<AircraftLookup> LoadAsync(ApplicationDbContext db, string userId, CancellationToken ct = default) =>
        new(db, userId,
            await db.Aircraft.Where(a => a.UserId == userId).ToListAsync(ct),
            await db.AircraftTypes.Where(t => t.UserId == userId).ToListAsync(ct));

    public Aircraft? Resolve(string? registration, string? typeName, string? manufacturer)
    {
        var reg = Aircraft.Normalize(registration);
        if (reg is null) return null;

        if (!_byReg.TryGetValue(reg, out var aircraft))
        {
            aircraft = new Aircraft { UserId = _userId, Registration = reg };
            _db.Aircraft.Add(aircraft);
            _byReg[reg] = aircraft;
        }

        if (aircraft.AircraftTypeId is null && aircraft.AircraftType is null && ResolveType(typeName, manufacturer) is { } type)
            aircraft.AircraftType = type;
        return aircraft;
    }

    public AircraftType? ResolveType(string? name, string? manufacturer)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        name = name.Trim();
        if (!_typeByName.TryGetValue(name, out var type))
        {
            type = new AircraftType { UserId = _userId, Name = name };
            _db.AircraftTypes.Add(type);
            _typeByName[name] = type;
        }
        if (type.Manufacturer is null && !string.IsNullOrWhiteSpace(manufacturer))
            type.Manufacturer = manufacturer.Trim().ToUpperInvariant();
        return type;
    }
}
