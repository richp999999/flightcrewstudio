using FlightCrewStudio.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Services.DayNight;

/// <summary>Recalculates day/night time and the night-landing flag for every stored flight (all users).</summary>
public sealed class DayNightRecalculator(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public sealed record Result(int Flights, int Updated, int Unchanged, int CannotCalculate, IReadOnlyList<string> MissingAirports);

    public async Task<Result> RecalculateAllAsync(Action<int, int>? progress = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ids = await db.Flights.OrderBy(f => f.Id).Select(f => f.Id).ToListAsync(ct);
        int updated = 0, unchanged = 0, cannot = 0;
        var missing = new SortedSet<string>();

        foreach (var chunk in ids.Chunk(200))
        {
            ct.ThrowIfCancellationRequested();
            await using var batch = await dbFactory.CreateDbContextAsync(ct);
            var flights = await batch.Flights.Include(f => f.DepartureAirport).Include(f => f.ArrivalAirport)
                .Where(f => chunk.Contains(f.Id)).ToListAsync(ct);
            foreach (var f in flights)
            {
                var r = FlightDayNight.Calculate(f.OffBlock, f.OnBlock, f.DepartureAirport, f.ArrivalAirport, out _, f.ArrivalTime);
                if (r is null)
                {
                    cannot++;
                    if (f.DepartureAirport is { Latitude: null } d) missing.Add(d.Icao);
                    if (f.ArrivalAirport is { Latitude: null } a) missing.Add(a.Icao);
                    continue;
                }
                if (f.TotalDayMinutes == r.Value.DayMinutes && f.TotalNightMinutes == r.Value.NightMinutes
                    && f.NightLanding == r.Value.NightLanding) { unchanged++; continue; }
                f.TotalDayMinutes = r.Value.DayMinutes;
                f.TotalNightMinutes = r.Value.NightMinutes;
                f.NightLanding = r.Value.NightLanding;
                updated++;
            }
            await batch.SaveChangesAsync(ct);
            progress?.Invoke(updated + unchanged + cannot, ids.Count);
        }
        return new Result(ids.Count, updated, unchanged, cannot, missing.ToList());
    }
}
