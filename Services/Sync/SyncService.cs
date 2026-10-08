using System.Collections.Concurrent;
using FlightCrewStudio.Data;
using FlightCrewStudio.Services;
using FlightCrewStudio.Services.DayNight;
using FlightCrewStudio.Services.GlobaLog;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Services.Sync;

public interface ISyncService
{
    /// <summary>Kick off a sync for the user on a background task. No-op if one is already running.</summary>
    void StartSync(string userId);

    /// <summary>Request cancellation of a running sync.</summary>
    void CancelSync(string userId);

    bool IsRunning(string userId);
}

public sealed class SyncService(
    IServiceScopeFactory scopeFactory,
    SyncProgressTracker tracker,
    ILogger<SyncService> logger) : ISyncService
{
    private const int PageSize = 100; // GlobaLog returns 100 flights per page
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _running = new();

    public bool IsRunning(string userId) => _running.ContainsKey(userId);

    public void CancelSync(string userId)
    {
        if (_running.TryGetValue(userId, out var cts)) cts.Cancel();
    }

    public void StartSync(string userId)
    {
        var cts = new CancellationTokenSource();
        if (!_running.TryAdd(userId, cts))
        {
            cts.Dispose();
            return; // already running
        }

        _ = Task.Run(() => RunAsync(userId, cts.Token))
            .ContinueWith(_ =>
            {
                if (_running.TryRemove(userId, out var done)) done.Dispose();
            });
    }

    private async Task RunAsync(string userId, CancellationToken ct)
    {
        var progress = tracker.GetOrCreate(userId);
        progress.Phase = SyncPhase.Connecting;
        progress.CurrentPage = 0;
        progress.TotalPages = 0;
        progress.FlightsSeen = 0;
        progress.NewFlights = 0;
        progress.UpdatedFlights = 0;
        progress.Error = null;
        progress.Message = "Connecting to GlobaLog…";
        progress.StartedAt = DateTime.UtcNow;
        progress.FinishedAt = null;
        progress.NotifyChanged();

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<IGlobaLogClientFactory>();

        try
        {
            var credential = await db.GlobaLogCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId, ct)
                ?? throw new GlobaLogException("No GlobaLog credentials saved. Add them in Settings first.");

            var state = await db.SyncStates.FirstOrDefaultAsync(s => s.UserId == userId, ct);
            if (state is null)
            {
                state = new SyncState { UserId = userId };
                db.SyncStates.Add(state);
                await db.SaveChangesAsync(ct);
            }

            var highWater = state.LastSyncedThrough;
            DateTime? newHighWater = highWater;

            // Everyone this user has flown with, keyed for matching ("#staffno" / "N:NAME").
            var crewIndex = (await db.CrewMembers.Where(c => c.UserId == userId).ToListAsync(ct))
                .Where(c => c.MatchKey() is not null)
                .ToDictionary(c => c.MatchKey()!);
            var roles = await CrewRoleLookup.LoadAsync(db, ct);
            var aircraft = await AircraftLookup.LoadAsync(db, userId, ct);
            var airports = await AirportLookup.LoadAsync(db, ct);
            var crewErrors = 0;
            var skippedEdited = 0;
            // Flights the user has edited here are left alone (by GlobaLog id).
            var editedIds = (await db.Flights.Where(x => x.UserId == userId && x.EditedLocally)
                .Select(x => x.GlobaLogFlightId).ToListAsync(ct)).ToHashSet();
            // Flights arrive newest first, so the first role seen for a person in this run is their latest.
            var roleSetThisRun = new HashSet<CrewMember>();

            using var client = factory.Create(credential);
            var hello = await client.TestConnectionAsync(ct);

            // Who am I in the crew lists? (Settings → "My crew member", else match my logbook name.)
            var configuredMe = credential.MyCrewMemberId is { } myId
                ? crewIndex.Values.FirstOrDefault(c => c.Id == myId)
                : null;
            var me = new MyCrewContext(configuredMe, hello.YourName);

            progress.Phase = SyncPhase.Fetching;
            progress.Message = "Fetching flights…";
            progress.NotifyChanged();

            var page = 1;
            var reachedAlreadySynced = false;

            while (!reachedAlreadySynced)
            {
                ct.ThrowIfCancellationRequested();

                var result = await client.GetFlightsPageAsync(page, ct);
                progress.CurrentPage = page;
                progress.TotalPages = result.Meta.TotalPages;
                progress.NotifyChanged();

                // Flights are ordered off-block descending. Once we hit one at/below the
                // high-water mark, everything after it is already synced.
                var toProcess = new List<GlobaLogFlight>();
                foreach (var f in result.Flights)
                {
                    if (highWater.HasValue && f.OffBlock.HasValue && f.OffBlock.Value <= highWater.Value)
                    {
                        reachedAlreadySynced = true;
                        break;
                    }
                    toProcess.Add(f);
                }

                for (var i = 0; i < toProcess.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var f = toProcess[i];
                    progress.Message = $"Page {page}: flight {i + 1} of {toProcess.Count} — {f.Name} {f.OffBlock:yyyy-MM-dd} (fetching crew)";
                    progress.NotifyChanged();

                    if (editedIds.Contains(f.Id))
                    {
                        skippedEdited++;
                        progress.FlightsSeen++;
                        if (f.OffBlock.HasValue && (!newHighWater.HasValue || f.OffBlock.Value > newHighWater.Value))
                            newHighWater = f.OffBlock.Value;
                        continue;
                    }

                    GlobaLogFlightDetails? details = null;
                    try
                    {
                        details = await client.GetFlightDetailsAsync(f, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        crewErrors++;
                        logger.LogWarning(ex, "Could not fetch crew for GlobaLog flight {FlightId}", f.Id);
                    }

                    var created = await UpsertAsync(db, userId, f, details, crewIndex, roles, aircraft, airports, roleSetThisRun, me, ct);
                    progress.FlightsSeen++;
                    if (created) progress.NewFlights++; else progress.UpdatedFlights++;

                    if (f.OffBlock.HasValue && (!newHighWater.HasValue || f.OffBlock.Value > newHighWater.Value))
                        newHighWater = f.OffBlock.Value;

                    // Save in batches so a long first sync keeps its progress if interrupted.
                    if ((i + 1) % 25 == 0)
                        await db.SaveChangesAsync(ct);
                }

                progress.Phase = SyncPhase.Saving;
                progress.NotifyChanged();
                await db.SaveChangesAsync(ct);
                progress.Phase = SyncPhase.Fetching;
                progress.NotifyChanged();

                if (result.Flights.Count == 0) break;
                if (result.Meta.TotalPages > 0 && page >= result.Meta.TotalPages) break;
                page++;
            }

            var summary = $"{progress.NewFlights} new, {progress.UpdatedFlights} updated" +
                          (skippedEdited > 0 ? $", {skippedEdited} edited here left unchanged" : "") +
                          (crewErrors > 0 ? $"; crew could not be loaded for {crewErrors} flight(s) — see log" : "") + ".";
            // Remember the crew member we recognised as "me" so Settings shows it.
            if (me.Discovered && me.Member is not null && credential.MyCrewMemberId is null)
            {
                var tracked = await db.GlobaLogCredentials.FirstAsync(c => c.UserId == userId, ct);
                tracked.MyCrewMemberId = me.Member.Id;
                summary += $" Identified you as {me.Member.DisplayName} in the crew lists (change in Settings).";
            }

            state.LastSyncedThrough = newHighWater;
            state.LastRunAt = DateTime.UtcNow;
            state.LastRunMessage = summary;
            await db.SaveChangesAsync(ct);

            progress.Phase = SyncPhase.Completed;
            progress.FinishedAt = DateTime.UtcNow;
            progress.Message = "Done — " + summary;
            progress.NotifyChanged();
        }
        catch (OperationCanceledException)
        {
            progress.Phase = SyncPhase.Failed;
            progress.FinishedAt = DateTime.UtcNow;
            progress.Error = "Sync cancelled.";
            progress.Message = "Cancelled.";
            progress.NotifyChanged();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync failed for user {UserId}", userId);
            progress.Phase = SyncPhase.Failed;
            progress.FinishedAt = DateTime.UtcNow;
            progress.Error = ex is GlobaLogException ? ex.Message : $"Sync failed: {ex.Message}";
            progress.Message = "Failed.";
            progress.NotifyChanged();
        }
    }

    /// <summary>
    /// Insert or update a flight by (userId, GlobaLog id) and, when <paramref name="details"/> are
    /// given, replace its crew links and fill in fuller airport details. Returns true if the flight was newly created.
    /// </summary>
    private static async Task<bool> UpsertAsync(
        ApplicationDbContext db, string userId, GlobaLogFlight f, GlobaLogFlightDetails? details,
        Dictionary<string, CrewMember> crewIndex, CrewRoleLookup roles, AircraftLookup aircraft, AirportLookup airports, HashSet<CrewMember> roleSetThisRun, MyCrewContext me, CancellationToken ct)
    {
        var existing = await db.Flights
            .Include(x => x.Crew)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.GlobaLogFlightId == f.Id, ct);

        // Fall back to the natural key, e.g. a flight stored earlier under a synthetic id.
        if (existing is null && f.OffBlock.HasValue && f.Name is not null)
        {
            var dep = f.Departure?.Airport?.Icao;
            existing = await db.Flights
                .Include(x => x.Crew)
                .FirstOrDefaultAsync(x => x.UserId == userId && x.OffBlock == f.OffBlock && x.Name == f.Name && x.DepartureAirport!.Icao == dep, ct);
            if (existing is not null) existing.GlobaLogFlightId = f.Id;
        }

        var isNew = existing is null;
        var flight = existing ?? new Flight { UserId = userId, GlobaLogFlightId = f.Id, FetchedAt = DateTime.UtcNow };

        flight.Name = f.Name;
        flight.Status = f.Status;
        flight.OffBlock = f.OffBlock;
        flight.DepartureTime = f.DepartureTime;
        flight.ArrivalTime = f.ArrivalTime;
        flight.OnBlock = f.OnBlock;
        flight.RadiationAmount = f.RadiationAmount;
        flight.TotalDayMinutes = f.TotalDayMinutes;
        flight.TotalNightMinutes = f.TotalNightMinutes;
        flight.TotalInstrMinutes = f.TotalInstrMinutes;
        flight.NightLanding = f.NightLanding;
        flight.ForeignId = f.ForeignId;

        // Airports: the list/CSV gives ICAO codes; the flight page adds IATA, name and country.
        static GlobaLogAirport? Merge(GlobaLogAirport? a, GlobaLogAirport? b) => a is null && b is null ? null : new GlobaLogAirport
        {
            Icao = a?.Icao ?? b?.Icao, Iata = a?.Iata ?? b?.Iata, Name = a?.Name ?? b?.Name, Country = a?.Country ?? b?.Country
        };
        var depInfo = Merge(f.Departure?.Airport, details?.Departure);
        var arrInfo = Merge(f.Arrival?.Airport, details?.Arrival);
        if (airports.Resolve(depInfo?.Icao, depInfo?.Iata, depInfo?.Name, depInfo?.Country) is { } depAirport)
            flight.DepartureAirport = depAirport;
        if (airports.Resolve(arrInfo?.Icao, arrInfo?.Iata, arrInfo?.Name, arrInfo?.Country) is { } arrAirport)
            flight.ArrivalAirport = arrAirport;

        // Day/night time: calculated here from the times and the great-circle route (when both
        // airports have positions), rather than taken from GlobaLog's export.
        FlightDayNight.Apply(flight, flight.DepartureAirport, flight.ArrivalAirport);

        // Role from the logbook export ("CPT", "PSV", …); replaced below by my crew-list role when known.
        if (roles.Resolve(CrewRoleLookup.FromLogbookCode(f.MyRole)) is { } exportRole)
            flight.MyCrewRole = exportRole;
        flight.PilotFlying = f.PilotFlying ?? flight.PilotFlying;

        if (aircraft.Resolve(f.Aircraft?.Registration, f.Aircraft?.Icao, f.Aircraft?.Manufacturer) is { } ac)
            flight.Aircraft = ac;

        flight.SourceCreatedAt = f.CreatedAt;
        flight.SourceUpdatedAt = f.UpdatedAt;
        flight.LastUpdatedAt = DateTime.UtcNow;

        var crew = details?.Crew;
        if (crew is not null)
        {
            if (flight.Crew.Count > 0)
                db.FlightCrew.RemoveRange(flight.Crew);
            flight.Crew = new List<FlightCrew>();

            var position = 0;
            foreach (var c in crew)
            {
                var member = FindOrCreateCrewMember(db, userId, c, crewIndex);
                if (member is null) continue;
                var role = roles.Resolve(c.Role);
                if (role is not null && roleSetThisRun.Add(member))
                    member.LatestCrewRole = role;
                flight.Crew.Add(new FlightCrew { CrewMember = member, CrewRole = role, Position = position++ });

                // My role on this flight comes from where I appear in its crew list.
                if (role is not null && me.IsMe(member))
                    flight.MyCrewRole = role;
            }
        }

        if (isNew) db.Flights.Add(flight);
        return isNew;
    }

    /// <summary>Match a crew entry to an existing person (by staff number or name), or create one.</summary>
    internal static CrewMember? FindOrCreateCrewMember(
        ApplicationDbContext db, string userId, GlobaLogCrew c, Dictionary<string, CrewMember> crewIndex)
    {
        var name = string.IsNullOrWhiteSpace(c.Name) ? null : CrewMember.CleanName(c.Name);
        var staffNo = string.IsNullOrWhiteSpace(c.LoadId) ? null : c.LoadId.Trim();

        // Prefer the staff number when we have one; otherwise the name.
        var key = CrewMember.MatchKey(staffNo is not null ? null : name, staffNo);
        if (key is null) return null;

        if (!crewIndex.TryGetValue(key, out var member))
        {
            member = new CrewMember
            {
                UserId = userId,
                Name = name,
                NormalizedName = name is null ? null : CrewMember.NormalizeName(name),
                StaffNumber = staffNo
            };
            db.CrewMembers.Add(member);
            crewIndex[key] = member;
        }
        else if (member.Name is null && name is not null)
        {
            member.Name = name; // learnt the name of someone previously known only by number
        }
        return member;
    }
}
