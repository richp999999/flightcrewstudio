using FlightCrewStudio.Components.Shared;
using FlightCrewStudio.Data;
using FlightCrewStudio.Services.DayNight;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Services.Export;

/// <summary>Which flights to export.</summary>
public sealed record ExportFilter(DateOnly? From, DateOnly? To, bool IncludePassive);

/// <summary>A flight-deck crew member as listed in the CSV's Person/Role columns.</summary>
public sealed record LogbookPerson(string Name, string Role);

/// <summary>
/// One logbook line, already worked out for export: times in UTC, minutes as whole numbers.
/// Shared by the EASA PDF and the CSV exporters so both show the same figures.
/// </summary>
public sealed record LogbookEntry
{
    public DateTime Date { get; init; }               // off-block date (UTC)
    public string? FlightNumber { get; init; }
    public string? Departure { get; init; }           // ICAO
    public DateTime? OffBlock { get; init; }
    public string? Arrival { get; init; }
    public DateTime? OnBlock { get; init; }
    public string? TypeCode { get; init; }            // ICAO designator, e.g. "A21N"
    public string? TypeManufacturer { get; init; }    // "Airbus"
    public string? TypeModel { get; init; }           // "A-321neo"
    public string? Registration { get; init; }        // "G-HLYH"

    public int TotalMinutes { get; init; }
    public int MultiPilotMinutes { get; init; }
    public int NightMinutes { get; init; }
    public int IfrMinutes { get; init; }
    public int PicMinutes { get; init; }
    public int CoPilotMinutes { get; init; }

    /// <summary>"Self" when the user was captain, otherwise the captain's name.</summary>
    public string? PicName { get; init; }
    public bool? PilotFlying { get; init; }
    public int TakeoffsDay { get; init; }
    public int TakeoffsNight { get; init; }
    public int LandingsDay { get; init; }
    public int LandingsNight { get; init; }
    public bool NightLanding { get; init; }
    public string? Remarks { get; init; }
    public List<LogbookPerson> FlightDeck { get; init; } = new();
}

/// <summary>Totals of the numeric logbook columns (EASA columns 5, 6, 8, 9, 10, 11).</summary>
public readonly record struct LogbookTotals(long MultiPilot, long Total, int LandingsDay, int LandingsNight,
    long Night, long Ifr, long Pic, long CoPilot, long SingleEngine = 0, long MultiEngine = 0, long Dual = 0,
    long Instructor = 0, long Simulator = 0)
{
    public static LogbookTotals operator +(LogbookTotals a, LogbookTotals b) => new(
        a.MultiPilot + b.MultiPilot, a.Total + b.Total, a.LandingsDay + b.LandingsDay, a.LandingsNight + b.LandingsNight,
        a.Night + b.Night, a.Ifr + b.Ifr, a.Pic + b.Pic, a.CoPilot + b.CoPilot, a.SingleEngine + b.SingleEngine,
        a.MultiEngine + b.MultiEngine, a.Dual + b.Dual, a.Instructor + b.Instructor, a.Simulator + b.Simulator);

    public static LogbookTotals Of(IEnumerable<LogbookEntry> entries) => entries.Aggregate(new LogbookTotals(), (t, e) => t + new LogbookTotals(
        e.MultiPilotMinutes, e.TotalMinutes, e.LandingsDay, e.LandingsNight, e.NightMinutes, e.IfrMinutes, e.PicMinutes, e.CoPilotMinutes));

    /// <summary>The opening balances from Settings → Previous Experience.</summary>
    public static LogbookTotals Of(PreviousExperience p) => new(
        p.MultiPilotMinutes, p.TotalMinutes, p.LandingsDay, p.LandingsNight, p.NightMinutes, p.IfrMinutes, p.PicMinutes,
        p.CoPilotMinutes, p.SingleEngineMinutes, p.MultiEngineMinutes, p.DualMinutes, p.InstructorMinutes, p.SimulatorMinutes);
}

public static class LogbookEntries
{
    /// <summary>
    /// The user's flights in logbook form, oldest first. The brought-forward totals are the user's
    /// previous experience plus the flights before <see cref="ExportFilter.From"/> (so the first
    /// page's "Total From Previous Pages" carries on from everything earlier).
    /// </summary>
    public static async Task<(List<LogbookEntry> Entries, LogbookTotals BroughtForward)> LoadAsync(
        ApplicationDbContext db, string userId, string? myName, ExportFilter filter, CancellationToken ct = default)
    {
        var myCrewId = await db.GlobaLogCredentials.AsNoTracking()
            .Where(c => c.UserId == userId).Select(c => c.MyCrewMemberId).FirstOrDefaultAsync(ct);

        var query = db.Flights.AsNoTracking().Where(f => f.UserId == userId && f.OffBlock != null);
        if (filter.To is { } to)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(f => f.OffBlock < end);
        }
        if (!filter.IncludePassive)
            query = query.Where(f => f.MyCrewRoleId == null || (f.MyCrewRoleId != CrewRole.PassiveId && f.MyCrewRoleId != CrewRole.CabinCrewId));

        var flights = await query
            .Include(f => f.Crew).ThenInclude(c => c.CrewMember)
            .Include(f => f.Crew).ThenInclude(c => c.CrewRole)
            .Include(f => f.Aircraft).ThenInclude(a => a!.AircraftType)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .AsSplitQuery()
            .OrderBy(f => f.OffBlock).ThenBy(f => f.Id)
            .ToListAsync(ct);

        var all = flights.Select(f => ToEntry(f, myCrewId, myName)).ToList();
        var opening = LogbookTotals.Of(await PreviousExperience.ForUserAsync(db, userId, ct));
        if (filter.From is not { } from) return (all, opening);

        var start = from.ToDateTime(TimeOnly.MinValue);
        return (all.Where(e => e.Date >= start).ToList(), opening + LogbookTotals.Of(all.Where(e => e.Date < start)));
    }

    private static LogbookEntry ToEntry(Flight f, int? myCrewId, string? myName)
    {
        var block = Fmt.BlockMinutes(f);
        var role = f.MyCrewRoleId;
        var isPic = role == CrewRole.CaptainId;
        var isCoPilot = role is CrewRole.FirstOfficerId or CrewRole.SecondOfficerId;

        var deck = f.Crew.Where(c => c.CrewRole?.IsFlightDeck == true).OrderBy(c => c.Position).ToList();
        var captain = deck.FirstOrDefault(c => c.CrewRoleId == CrewRole.CaptainId && c.CrewMemberId != myCrewId);
        var picName = isPic ? "Self" : captain?.CrewMember?.DisplayName;

        // Take-offs and landings are logged by the pilot flying. GlobaLog says whether the landing
        // was at night; for the take-off, use the sun at the departure airfield at take-off time.
        var pf = f.PilotFlying == true;
        var nightTakeoff = pf && f.DepartureAirport is { Latitude: { } lat, Longitude: { } lon }
            && DayNightCalculator.IsNight(DateTime.SpecifyKind((f.DepartureTime ?? f.OffBlock)!.Value, DateTimeKind.Utc), lat, lon);

        var type = f.Aircraft?.AircraftType;
        return new LogbookEntry
        {
            Date = f.OffBlock!.Value.Date,
            FlightNumber = f.Name,
            Departure = f.DepartureAirport?.Icao,
            OffBlock = f.OffBlock,
            Arrival = f.ArrivalAirport?.Icao,
            OnBlock = f.OnBlock,
            TypeCode = type?.Designator,
            TypeManufacturer = type?.Manufacturer is { Length: > 0 } m ? TitleCase(m) : null,
            TypeModel = type?.Name,
            Registration = f.Aircraft?.DisplayRegistration,
            TotalMinutes = block,
            MultiPilotMinutes = block,           // airline flying: every aircraft is multi-pilot
            NightMinutes = Math.Min(f.TotalNightMinutes, block),
            IfrMinutes = Math.Min(f.TotalInstrMinutes, block),
            PicMinutes = isPic ? block : 0,
            CoPilotMinutes = isCoPilot ? block : 0,
            PicName = picName,
            PilotFlying = f.PilotFlying,
            TakeoffsDay = pf && !nightTakeoff ? 1 : 0,
            TakeoffsNight = nightTakeoff ? 1 : 0,
            LandingsDay = pf && !f.NightLanding ? 1 : 0,
            LandingsNight = pf && f.NightLanding ? 1 : 0,
            NightLanding = f.NightLanding,
            Remarks = f.Remarks,
            FlightDeck = deck.Select(c => new LogbookPerson(
                    c.CrewMemberId == myCrewId && !string.IsNullOrWhiteSpace(myName) ? myName : c.CrewMember?.DisplayName ?? "",
                    c.CrewRoleId == CrewRole.CaptainId ? "PIC" : "COP"))
                .Where(p => p.Name.Length > 0)
                .Take(3)
                .ToList(),
        };
    }

    /// <summary>"AIRBUS" → "Airbus".</summary>
    private static string TitleCase(string s) =>
        System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
}
