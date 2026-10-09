using FlightCrewStudio.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Services;

/// <summary>
/// Resolves registrations to the user's <see cref="Aircraft"/> rows during a sync or import,
/// adding new aircraft as they're met. (The aircraft's ICAO type is set separately, from
/// <see cref="IcaoTypeLookup"/>.) One per unit of work.
/// </summary>
public sealed class AircraftLookup
{
    private readonly ApplicationDbContext _db;
    private readonly string _userId;
    private readonly Dictionary<string, Aircraft> _byReg;

    private AircraftLookup(ApplicationDbContext db, string userId, IEnumerable<Aircraft> aircraft)
    {
        _db = db;
        _userId = userId;
        _byReg = aircraft.ToDictionary(a => a.Registration, StringComparer.OrdinalIgnoreCase);
    }

    public static async Task<AircraftLookup> LoadAsync(ApplicationDbContext db, string userId, CancellationToken ct = default) =>
        new(db, userId, await db.Aircraft.Where(a => a.UserId == userId).ToListAsync(ct));

    public Aircraft? Resolve(string? registration)
    {
        var reg = Aircraft.Normalize(registration);
        if (reg is null) return null;

        if (!_byReg.TryGetValue(reg, out var aircraft))
        {
            aircraft = new Aircraft { UserId = _userId, Registration = reg };
            _db.Aircraft.Add(aircraft);
            _byReg[reg] = aircraft;
        }
        return aircraft;
    }
}
