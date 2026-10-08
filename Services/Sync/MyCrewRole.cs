using FlightCrewStudio.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Services.Sync;

/// <summary>
/// Works out which crew member is "me" during a sync. Either the one chosen in Settings, or —
/// if none is chosen yet — the crew member whose name matches the name on the user's GlobaLog
/// logbook (discovered as flights are processed, then saved to Settings).
/// </summary>
public sealed class MyCrewContext
{
    public CrewMember? Member { get; private set; }
    private readonly string? _nameKey;
    public bool Discovered { get; private set; }

    public MyCrewContext(CrewMember? configured, string? logbookName)
    {
        Member = configured;
        _nameKey = configured is null && !string.IsNullOrWhiteSpace(logbookName)
            ? CrewMember.MatchKey(logbookName, null)
            : null;
    }

    public bool IsMe(CrewMember m)
    {
        if (Member is not null) return ReferenceEquals(m, Member) || (m.Id != 0 && m.Id == Member.Id);
        if (_nameKey is not null && m.MatchKey() == _nameKey)
        {
            Member = m;
            Discovered = true;
            return true;
        }
        return false;
    }
}

public static class MyCrewRole
{
    /// <summary>
    /// Set <see cref="Flight.MyCrewRoleId"/> on every stored flight the given crew member was on, from
    /// the role recorded in that flight's crew list. Returns the number of flights updated.
    /// </summary>
    public static async Task<int> ApplyToExistingFlightsAsync(ApplicationDbContext db, string userId, int crewMemberId, CancellationToken ct = default)
    {
        var links = await db.FlightCrew
            .Where(fc => fc.CrewMemberId == crewMemberId && fc.Flight!.UserId == userId && fc.CrewRoleId != null)
            .Select(fc => new { fc.FlightId, fc.CrewRoleId, fc.Position })
            .ToListAsync(ct);
        var roleByFlight = links.GroupBy(l => l.FlightId).ToDictionary(g => g.Key, g => g.OrderBy(x => x.Position).First().CrewRoleId);

        var ids = roleByFlight.Keys.ToList();
        var flights = await db.Flights.Where(f => ids.Contains(f.Id)).ToListAsync(ct);
        var changed = 0;
        foreach (var f in flights)
        {
            var role = roleByFlight[f.Id];
            if (f.MyCrewRoleId != role) { f.MyCrewRoleId = role; changed++; }
        }
        await db.SaveChangesAsync(ct);
        return changed;
    }
}
