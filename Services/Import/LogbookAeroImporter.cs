using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FlightCrewStudio.Data;
using FlightCrewStudio.Services.GlobaLog;
using FlightCrewStudio.Services.Sync;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Services.Import;

/// <summary>A person from a row's PersonN / RoleN columns (or PIC_Name).</summary>
public sealed record ImportPerson(string Name, string? RoleCode);

/// <summary>One flight read from a logbook.aero CSV export.</summary>
public sealed class ImportedFlight
{
    public int Line { get; init; }                       // line number in the file (header = 1)
    public DateTime OffBlock { get; init; }              // UTC
    public DateTime OnBlock { get; init; }
    public string? FlightNumber { get; init; }
    public string Departure { get; init; } = "";           // ICAO code, or the airfield as written when it isn't one
    public string Arrival { get; init; } = "";
    public string? TypeCode { get; init; }               // "A21N"
    public string? TypeManufacturer { get; init; }       // "Airbus"
    public string? TypeModel { get; init; }              // "A-321neo"
    public string? Registration { get; init; }
    public int TotalMinutes { get; init; }
    public int NightMinutes { get; init; }
    public int IfrMinutes { get; init; }
    public int PicMinutes { get; init; }
    public int CoPilotMinutes { get; init; }
    public int MultiPilotMinutes { get; init; }
    public int DualMinutes { get; init; }
    public int InstructorMinutes { get; init; }
    public int TakeoffsDay { get; init; }
    public int TakeoffsNight { get; init; }
    public string? PicName { get; init; }
    public int Takeoffs { get; init; }
    public int LandingsDay { get; init; }
    public int LandingsNight { get; init; }
    public bool PilotFlying { get; init; }
    public string? Remarks { get; init; }
    public bool LocalTime { get; init; }                 // Timezone "L"
    public List<ImportPerson> People { get; init; } = new();

    /// <summary>Set by the preview: an equivalent flight is already stored, so this row is skipped.</summary>
    public bool AlreadyStored { get; set; }
}

public sealed record ImportProblem(int Line, string Message);

public sealed class ImportPreview
{
    public List<ImportedFlight> Flights { get; } = new();
    public List<ImportProblem> Problems { get; } = new();
    public int PersonColumns { get; set; }
    public IEnumerable<ImportedFlight> New => Flights.Where(f => !f.AlreadyStored);
    public int AlreadyStored => Flights.Count(f => f.AlreadyStored);
    public int LocalTimeRows => Flights.Count(f => f.LocalTime && !f.AlreadyStored);
}

/// <summary>
/// Imports flights from logbook.aero's CSV export (the same layout FlightCrewStudio exports).
/// The file is read in two steps: <see cref="PreviewAsync"/> parses it and marks rows that are
/// already stored; <see cref="ImportAsync"/> then adds the new flights.
///
/// Crew come from every PersonN / RoleN column pair in the file — Person1…Person3 normally, but
/// any number is accepted. Flights already in the app (same departure airport, off-block within
/// <see cref="MatchWindow"/>, see <see cref="FlightMatching"/>) are skipped, so GlobaLog-synced flights stay as they are and
/// importing the same file twice adds nothing.
/// </summary>
public static partial class LogbookAeroImporter
{
    public const string Source = "logbook.aero";
    public static TimeSpan MatchWindow => FlightMatching.Window;

    private static readonly string[] Required = ["Date", "Departure_Airfield", "Departure_Time", "Arrival_Airfield", "Total_Time"];

    [GeneratedRegex(@"^Person(\d+)$", RegexOptions.IgnoreCase)] private static partial Regex PersonColumn();

    // ---- parsing ---------------------------------------------------------------------------

    public static async Task<ImportPreview> PreviewAsync(ApplicationDbContext db, string userId, string csvText, CancellationToken ct = default)
    {
        var preview = Parse(csvText);
        if (preview.Flights.Count == 0) return preview;

        // Existing flights, by departure airport, to spot rows that are already stored.
        var from = preview.Flights.Min(f => f.OffBlock) - MatchWindow;
        var to = preview.Flights.Max(f => f.OffBlock) + MatchWindow;
        var existing = (await db.Flights.AsNoTracking()
                .Where(f => f.UserId == userId && f.OffBlock >= from && f.OffBlock <= to)
                .Select(f => new { f.OffBlock, Dep = f.DepartureAirport!.Icao })
                .ToListAsync(ct))
            .GroupBy(x => FlightMatching.Key(x.Dep))
            .ToDictionary(g => g.Key, g => g.Select(x => x.OffBlock!.Value).ToList());

        // Same rule as sync uses: same departure airport, off-block within the window.
        var seenInFile = new List<ImportedFlight>();
        foreach (var f in preview.Flights)
        {
            var stored = existing.TryGetValue(FlightMatching.Key(f.Departure), out var times)
                         && times.Any(t => FlightMatching.IsMatch(f.Departure, t, f.Departure, f.OffBlock));
            var dupInFile = seenInFile.Any(s => FlightMatching.IsMatch(s.Departure, s.OffBlock, f.Departure, f.OffBlock));
            f.AlreadyStored = stored || dupInFile;
            if (!f.AlreadyStored) seenInFile.Add(f);
        }
        return preview;
    }

    /// <summary>Airfields that aren't ICAO codes (e.g. "Doncaster Sheffield") are stored without an airport.</summary>
    private static bool IsIcao(string s) => FlightMatching.Key(s) == s;

    public static ImportPreview Parse(string text)
    {
        var preview = new ImportPreview();
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        var rows = Csv.Parse(text);
        if (rows.Count == 0) throw new FormatException("The file is empty.");

        var header = rows[0].Select(h => h.Trim()).ToList();
        var col = header.Select((h, i) => (h, i)).GroupBy(x => x.h, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);
        var missing = Required.Where(c => !col.ContainsKey(c)).ToList();
        if (missing.Count > 0)
            throw new FormatException($"This doesn't look like a logbook.aero CSV export — missing column(s): {string.Join(", ", missing)}.");

        // PersonN / RoleN pairs, however many the file has.
        var personCols = header
            .Select((h, i) => (m: PersonColumn().Match(h), i))
            .Where(x => x.m.Success)
            .Select(x => (n: int.Parse(x.m.Groups[1].Value), personIdx: x.i, roleIdx: col.TryGetValue($"Role{x.m.Groups[1].Value}", out var r) ? r : -1))
            .OrderBy(x => x.n)
            .ToList();
        preview.PersonColumns = personCols.Count;

        for (var i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            var line = i + 1;
            if (r.All(string.IsNullOrWhiteSpace)) continue;

            string? Get(string name) => col.TryGetValue(name, out var idx) && idx < r.Count && !string.IsNullOrWhiteSpace(r[idx]) ? r[idx].Trim() : null;
            int Count(string name) => int.TryParse(Get(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : 0;

            try
            {
                if (!DateTime.TryParseExact(Get("Date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    throw new FormatException($"date '{Get("Date")}' isn't yyyy-MM-dd");
                var dep = Airfield(Get("Departure_Airfield")) ?? throw new FormatException("no departure airfield");
                var arr = Airfield(Get("Arrival_Airfield")) ?? throw new FormatException("no arrival airfield");
                var depTime = ParseClock(Get("Departure_Time"), "departure time");
                var arrTime = ParseClock(Get("Arrival_Time"), "arrival time");
                var total = ParseDuration(Get("Total_Time"), "Total_Time");

                var off = DateTime.SpecifyKind(date + (depTime ?? TimeSpan.Zero), DateTimeKind.Utc);
                // On-block: off-block plus the block time; failing that, the arrival time (next day if it's earlier).
                DateTime on;
                if (total > 0) on = off.AddMinutes(total);
                else if (arrTime is { } a) { on = DateTime.SpecifyKind(date + a, DateTimeKind.Utc); if (on < off) on = on.AddDays(1); total = (int)(on - off).TotalMinutes; }
                else throw new FormatException("no Total_Time or arrival time");

                var type = SplitType(Get("Aircraft_Type"));
                var pf = Get("Pilot_Flying") == "1";
                var takeoffs = Count("Takeoff_Day") + Count("Takeoff_Night");
                var ldgDay = Count("Landing_Day");
                var ldgNight = Count("Landing_Night");

                var people = new List<ImportPerson>();
                foreach (var (_, pIdx, rIdx) in personCols)
                {
                    var name = pIdx < r.Count ? r[pIdx].Trim() : "";
                    if (name.Length == 0) continue;
                    var role = rIdx >= 0 && rIdx < r.Count && r[rIdx].Trim().Length > 0 ? r[rIdx].Trim() : null;
                    people.Add(new ImportPerson(name, role));
                }

                preview.Flights.Add(new ImportedFlight
                {
                    Line = line,
                    OffBlock = off,
                    OnBlock = on,
                    FlightNumber = Get("Flight_Number")?.ToUpperInvariant(),
                    Departure = dep,
                    Arrival = arr,
                    TypeCode = type.Code, TypeManufacturer = type.Maker, TypeModel = type.Model,
                    Registration = Get("Aircraft_Registration"),
                    TotalMinutes = total,
                    NightMinutes = Math.Min(total, ParseDuration(Get("Night_Time"), "Night_Time")),
                    IfrMinutes = Math.Min(total, ParseDuration(Get("IFR_Time"), "IFR_Time")),
                    PicMinutes = ParseDuration(Get("PIC_Time"), "PIC_Time"),
                    CoPilotMinutes = ParseDuration(Get("CoPilot_Time"), "CoPilot_Time"),
                    MultiPilotMinutes = Math.Min(total, ParseDuration(Get("MultiPilot_Time"), "MultiPilot_Time")),
                    DualMinutes = ParseDuration(Get("Dual_Time"), "Dual_Time"),
                    InstructorMinutes = ParseDuration(Get("Instructor_Time"), "Instructor_Time"),
                    TakeoffsDay = Count("Takeoff_Day"),
                    TakeoffsNight = Count("Takeoff_Night"),
                    PicName = Get("PIC_Name"),
                    Takeoffs = takeoffs,
                    LandingsDay = ldgDay,
                    LandingsNight = ldgNight,
                    // Pilot flying if the file says so or the row logs take-offs/landings (logbook.aero
                    // often leaves Pilot_Flying at 0 even then); otherwise pilot monitoring.
                    PilotFlying = pf || takeoffs + ldgDay + ldgNight > 0,
                    Remarks = Get("Remarks") is { } rem && rem.Length > 1000 ? rem[..1000] : Get("Remarks"),
                    LocalTime = string.Equals(Get("Timezone"), "L", StringComparison.OrdinalIgnoreCase),
                    People = people,
                });
            }
            catch (FormatException ex)
            {
                preview.Problems.Add(new ImportProblem(line, ex.Message));
            }
        }
        return preview;
    }

    /// <summary>"egbb" → "EGBB"; anything that isn't an ICAO code is kept as written.</summary>
    private static string? Airfield(string? s) =>
        s is null ? null : IsIcao(s.Trim().ToUpperInvariant()) ? s.Trim().ToUpperInvariant() : s.Trim();

    private static TimeSpan? ParseClock(string? s, string what)
    {
        if (s is null) return null;
        if (TimeSpan.TryParseExact(s, ["h\\:mm", "hh\\:mm"], CultureInfo.InvariantCulture, out var t) && t < TimeSpan.FromDays(1)) return t;
        throw new FormatException($"{what} '{s}' isn't HH:MM");
    }

    private static int ParseDuration(string? s, string what)
    {
        if (s is null) return 0;
        var parts = s.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m) && h >= 0 && m is >= 0 and < 60)
            return h * 60 + m;
        throw new FormatException($"{what} '{s}' isn't H:MM");
    }

    /// <summary>"A21N,Airbus,A-321neo" → ("A21N", "Airbus", "A-321neo").</summary>
    private static (string? Code, string? Maker, string? Model) SplitType(string? s)
    {
        if (s is null) return (null, null, null);
        var p = s.Split(',').Select(x => x.Trim()).ToArray();
        string? At(int i) => i < p.Length && p[i].Length > 0 ? p[i] : null;
        var code = At(0);
        return (code is { Length: <= 4 } ? code.ToUpperInvariant() : null, At(1), At(2) ?? code);
    }

    // ---- import ----------------------------------------------------------------------------

    public sealed record Result(int Added, int Skipped, int NewCrew, int NewAircraft, int NewAirports);

    public static async Task<Result> ImportAsync(ApplicationDbContext db, string userId, ImportPreview preview, CancellationToken ct = default)
    {
        var toAdd = preview.New.ToList();
        var crewIndex = (await db.CrewMembers.Where(c => c.UserId == userId).ToListAsync(ct))
            .Where(c => c.MatchKey() is not null)
            .ToDictionary(c => c.MatchKey()!);
        var crewBefore = crewIndex.Count;
        var roles = await CrewRoleLookup.LoadAsync(db, ct);
        var aircraft = await AircraftLookup.LoadAsync(db, userId, ct);
        var airports = await AirportLookup.LoadAsync(db, ct);
        var icaoTypes = await IcaoTypeLookup.LoadAsync(db, ct);
        var aircraftBefore = await db.Aircraft.CountAsync(a => a.UserId == userId, ct);
        var airportsBefore = await db.Airports.CountAsync(ct);

        // Who am I? The crew member chosen in Settings, else the name printed on logbook exports.
        var cred = await db.GlobaLogCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId, ct);
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var myMember = cred?.MyCrewMemberId is { } myId ? crewIndex.Values.FirstOrDefault(c => c.Id == myId) : null;
        var myNames = new[] { myMember?.Name, user.LogbookName, user.DisplayName }
            .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => CrewMember.NormalizeName(n!)).Distinct().ToList();

        // GlobaLog's own ids are positive; imported flights get a stable negative id from their key.
        var usedIds = (await db.Flights.Where(f => f.UserId == userId && f.GlobaLogFlightId < 0)
            .Select(f => f.GlobaLogFlightId).ToListAsync(ct)).ToHashSet();

        foreach (var f in toAdd)
        {
            var id = ImportId(f);
            while (!usedIds.Add(id)) id--;

            var flight = new Flight
            {
                UserId = userId,
                GlobaLogFlightId = id,
                Source = Source,
                Name = f.FlightNumber,
                OffBlock = f.OffBlock,
                OnBlock = f.OnBlock,
                TotalDayMinutes = f.TotalMinutes - f.NightMinutes,
                TotalNightMinutes = f.NightMinutes,
                TotalInstrMinutes = f.IfrMinutes,
                NightLanding = f.LandingsNight > 0,
                PilotFlying = f.PilotFlying,
                Remarks = RemarksWithAirfields(f),
                DepartureAirport = IsIcao(f.Departure) ? airports.Resolve(f.Departure) : null,
                ArrivalAirport = IsIcao(f.Arrival) ? airports.Resolve(f.Arrival) : null,
                // Keep logbook.aero's own figures, so totals match it exactly. Time not logged as
                // multi-pilot was single-pilot; airliners are multi-engine, so it counts as ME.
                LoggedMultiPilotMinutes = f.MultiPilotMinutes,
                LoggedMultiEngineMinutes = f.TotalMinutes - f.MultiPilotMinutes,
                LoggedSingleEngineMinutes = 0,
                LoggedPicMinutes = f.PicMinutes,
                LoggedCoPilotMinutes = f.CoPilotMinutes,
                LoggedDualMinutes = f.DualMinutes,
                LoggedInstructorMinutes = f.InstructorMinutes,
                TakeoffsDay = f.TakeoffsDay,
                TakeoffsNight = f.TakeoffsNight,
                LandingsDay = f.LandingsDay,
                LandingsNight = f.LandingsNight,
                FetchedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
            };

            if (aircraft.Resolve(f.Registration) is { } ac)
            {
                flight.Aircraft = ac;
                // The aircraft's ICAO type, by the file's designator ("A21N"), when it hasn't one yet.
                if (ac.IcaoAircraftTypeId is null && icaoTypes.Find(f.TypeCode, f.TypeManufacturer, f.TypeModel) is { } icaoTypeId)
                    ac.IcaoAircraftTypeId = icaoTypeId;
            }

            // Crew, in column order. A captain named only in PIC_Name is added too.
            var people = f.People.Select(p => p with { Name = StripMyName(p.Name, myNames) }).ToList();
            if (f.PicName is { } picName && !picName.Equals("Self", StringComparison.OrdinalIgnoreCase)
                && !people.Any(p => RoleName(p.RoleCode) == "Captain"))
                people.Insert(0, new ImportPerson(StripMyName(picName, myNames), "PIC"));

            var position = 0;
            CrewRole? myRoleFromCrew = null;
            foreach (var p in people)
            {
                if (string.IsNullOrWhiteSpace(p.Name)) continue;
                var isMe = myNames.Contains(CrewMember.NormalizeName(p.Name));
                var member = isMe && myMember is not null
                    ? myMember
                    : SyncService.FindOrCreateCrewMember(db, userId, new GlobaLogCrew { Name = p.Name }, crewIndex);
                if (member is null || flight.Crew.Any(c => c.CrewMember == member)) continue;
                var role = roles.Resolve(RoleName(p.RoleCode), flightDeck: true);
                flight.Crew.Add(new FlightCrew { CrewMember = member, CrewRole = role, Position = position++ });
                if (isMe) myRoleFromCrew = role;
            }

            // My role: from the logged function times, else from where I appear in the crew.
            flight.MyCrewRole = f.PicMinutes > 0 ? roles.Resolve("Captain")
                : f.CoPilotMinutes > 0 ? roles.Resolve("First officer")
                : myRoleFromCrew
                ?? (string.Equals(f.PicName, "Self", StringComparison.OrdinalIgnoreCase) ? roles.Resolve("Captain") : null);

            db.Flights.Add(flight);
        }

        await db.SaveChangesAsync(ct);
        return new Result(toAdd.Count, preview.AlreadyStored, crewIndex.Count - crewBefore,
            await db.Aircraft.CountAsync(a => a.UserId == userId, ct) - aircraftBefore,
            await db.Airports.CountAsync(ct) - airportsBefore);
    }

    /// <summary>Airfields that aren't ICAO codes can't be linked to an airport, so keep them in the remarks.</summary>
    private static string? RemarksWithAirfields(ImportedFlight f)
    {
        var notes = new List<string>();
        if (!IsIcao(f.Departure)) notes.Add($"From: {f.Departure}");
        if (!IsIcao(f.Arrival)) notes.Add($"To: {f.Arrival}");
        var text = string.Join(". ", new[] { f.Remarks }.Concat(notes).Where(s => !string.IsNullOrWhiteSpace(s)));
        return text.Length == 0 ? null : text.Length > 1000 ? text[..1000] : text;
    }

    /// <summary>logbook.aero role codes → crew role names.</summary>
    public static string? RoleName(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        null or "" => null,
        "PIC" or "CPT" or "CAPT" => "Captain",
        "COP" or "SIC" or "FO" => "First officer",
        "SO" => "Second officer",
        "INS" => "Instructor",
        "EXM" => "Examiner",
        "SNY" => "Supernumerary",
        "PICUS" => "PIC under supervision",
        "DUAL" or "STU" => "Student",
        _ => code.Trim(),
    };

    /// <summary>Crew role names → logbook.aero role codes (for the CSV export).</summary>
    public static string RoleCode(string? roleName) => roleName switch
    {
        "Captain" => "PIC",
        "First officer" or "Second officer" => "COP",
        "Instructor" => "INS",
        "Examiner" => "EXM",
        "Supernumerary" => "SNY",
        "PIC under supervision" => "PICUS",
        "Student" => "DUAL",
        _ => "COP",
    };

    /// <summary>
    /// Some logbook.aero entries (brought in from GlobaLog) carry the user's name glued to the
    /// other pilot's, e.g. "Richard PALMER James DENNIS" → "James DENNIS".
    /// </summary>
    private static string StripMyName(string name, List<string> myNames)
    {
        var clean = CrewMember.CleanName(name);
        foreach (var me in myNames)
            if (clean.Length > me.Length + 1 && clean.ToUpperInvariant().StartsWith(me + " "))
                return clean[(me.Length + 1)..];
        return clean;
    }

    /// <summary>Stable negative id from the flight's natural key, so it can't clash with GlobaLog ids.</summary>
    private static long ImportId(ImportedFlight f)
    {
        var key = $"lba|{f.OffBlock:yyyy-MM-ddTHH:mm}|{f.Departure}|{f.Arrival}|{f.FlightNumber}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return -((BitConverter.ToInt64(hash, 0) & long.MaxValue) | 1);
    }
}
