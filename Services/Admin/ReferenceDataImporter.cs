using System.Globalization;
using FlightCrewStudio.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Services.Admin;

/// <summary>Outcome of a CSV import, shown on the admin page.</summary>
public sealed record ImportResult(int Added, int Updated, int Skipped, IReadOnlyList<string> Notes)
{
    public string Summary => $"{Added:N0} added" + (Updated > 0 ? $", {Updated:N0} updated" : "") + (Skipped > 0 ? $", {Skipped:N0} skipped" : "") + ".";
}

/// <summary>
/// Imports the OurAirports reference files (countries.csv, regions.csv, airports.csv).
/// Rows are matched on their code (country code, region code, airport ident) and updated in
/// place, so re-importing a newer file refreshes the data without creating duplicates and without
/// breaking links from flights. Nothing is deleted. Import countries first, then regions, then
/// airports, so their country/region links can be resolved.
/// </summary>
public sealed class ReferenceDataImporter(IDbContextFactory<ApplicationDbContext> dbFactory, ILogger<ReferenceDataImporter> logger)
{
    /// <summary>Largest file accepted from the browser (airports.csv is ~11 MB).</summary>
    public const long MaxFileBytes = 64L * 1024 * 1024;

    public static readonly string[] AirportTypes =
        ["large_airport", "medium_airport", "small_airport", "heliport", "seaplane_base", "balloonport", "closed"];

    private const int BatchSize = 1000;

    private static string? Cut(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];

    // ------------------------------------------------------------------ countries

    public async Task<ImportResult> ImportCountriesAsync(string csv, CancellationToken ct = default)
    {
        var t = new Csv.Table(csv);
        t.Require("countries", "code", "name");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = await db.Countries.ToDictionaryAsync(c => c.Code, StringComparer.OrdinalIgnoreCase, ct);
        int added = 0, updated = 0, skipped = 0;

        foreach (var row in t.Rows)
        {
            var code = t.Get(row, "code")?.ToUpperInvariant();
            var name = t.Get(row, "name");
            if (code is null || code.Length != 2 || name is null) { skipped++; continue; }

            if (!existing.TryGetValue(code, out var c))
            {
                c = new Country { Code = code, Name = name };
                db.Countries.Add(c);
                existing[code] = c;
                added++;
            }
            else updated++;

            c.Name = Cut(name, 100)!;
            c.Continent = Cut(t.Get(row, "continent"), 2);
            c.WikipediaLink = Cut(t.Get(row, "wikipedia_link"), 255);
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Countries import: {Added} added, {Updated} updated, {Skipped} skipped", added, updated, skipped);
        return new ImportResult(added, updated, skipped, []);
    }

    // ------------------------------------------------------------------ regions

    public async Task<ImportResult> ImportRegionsAsync(string csv, CancellationToken ct = default)
    {
        var t = new Csv.Table(csv);
        t.Require("regions", "code", "name", "iso_country");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var countries = await db.Countries.AsNoTracking().ToDictionaryAsync(c => c.Code, c => c.Id, StringComparer.OrdinalIgnoreCase, ct);
        var existing = await db.Regions.ToDictionaryAsync(r => r.Code, StringComparer.OrdinalIgnoreCase, ct);
        int added = 0, updated = 0, skipped = 0, noCountry = 0;

        foreach (var row in t.Rows)
        {
            var code = t.Get(row, "code")?.ToUpperInvariant();
            var name = t.Get(row, "name");
            if (code is null || code.Length > 10 || name is null) { skipped++; continue; }

            if (!existing.TryGetValue(code, out var r))
            {
                r = new Region { Code = code, Name = name };
                db.Regions.Add(r);
                existing[code] = r;
                added++;
            }
            else updated++;

            r.Name = Cut(name, 100)!;
            r.LocalCode = Cut(t.Get(row, "local_code"), 10);
            r.Continent = Cut(t.Get(row, "continent"), 2);
            r.WikipediaLink = Cut(t.Get(row, "wikipedia_link"), 255);
            var iso = t.Get(row, "iso_country");
            if (iso is not null && countries.TryGetValue(iso, out var countryId)) r.CountryId = countryId;
            else noCountry++;
        }
        await db.SaveChangesAsync(ct);

        var notes = new List<string>();
        if (noCountry > 0) notes.Add($"{noCountry:N0} region(s) refer to a country that isn't loaded — import countries first, then re-import regions.");
        return new ImportResult(added, updated, skipped, notes);
    }

    // ------------------------------------------------------------------ airports

    /// <param name="types">OurAirports types to import (e.g. large_airport, medium_airport). Others are skipped.</param>
    /// <param name="progress">Called after each batch with (rows processed, rows to process).</param>
    public async Task<ImportResult> ImportAirportsAsync(string csv, IReadOnlySet<string> types,
        Action<int, int>? progress = null, CancellationToken ct = default)
    {
        var t = new Csv.Table(csv);
        t.Require("airports", "ident", "type", "name", "iso_country", "iso_region");

        Dictionary<string, int> countries, regions;
        await using (var db0 = await dbFactory.CreateDbContextAsync(ct))
        {
            countries = await db0.Countries.AsNoTracking().ToDictionaryAsync(c => c.Code, c => c.Id, StringComparer.OrdinalIgnoreCase, ct);
            regions = await db0.Regions.AsNoTracking().ToDictionaryAsync(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase, ct);
        }

        var wanted = new List<List<string>>();
        int skipped = 0;
        foreach (var row in t.Rows)
        {
            var ident = t.Get(row, "ident");
            var type = t.Get(row, "type");
            if (ident is null || ident.Length > 10 || type is null || !types.Contains(type)) { skipped++; continue; }
            wanted.Add(row);
        }

        int added = 0, updated = 0, missingCountry = 0, missingRegion = 0;
        for (var start = 0; start < wanted.Count; start += BatchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = wanted.Skip(start).Take(BatchSize).ToList();
            var idents = batch.Select(r => t.Get(r, "ident")!.ToUpperInvariant()).ToList();

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var existing = await db.Airports.Where(a => idents.Contains(a.Icao)).ToDictionaryAsync(a => a.Icao, StringComparer.OrdinalIgnoreCase, ct);

            foreach (var row in batch)
            {
                var ident = t.Get(row, "ident")!.ToUpperInvariant();
                if (!existing.TryGetValue(ident, out var a))
                {
                    a = new Airport { Icao = ident };
                    db.Airports.Add(a);
                    existing[ident] = a;
                    added++;
                }
                else updated++;

                a.Name = Cut(t.Get(row, "name"), 150);
                a.Municipality = Cut(t.Get(row, "municipality"), 100) ?? a.Municipality;
                a.Type = Cut(t.Get(row, "type"), 20);
                var iata = t.Get(row, "iata_code")?.ToUpperInvariant();
                if (iata is { Length: 3 }) a.Iata = iata;
                a.Latitude = double.TryParse(t.Get(row, "latitude_deg"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ? lat : a.Latitude;
                a.Longitude = double.TryParse(t.Get(row, "longitude_deg"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ? lon : a.Longitude;
                a.ElevationFt = int.TryParse(t.Get(row, "elevation_ft"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var elev) ? elev : a.ElevationFt;

                var iso = t.Get(row, "iso_country");
                if (iso is not null && countries.TryGetValue(iso, out var cId)) a.CountryId = cId; else missingCountry++;
                var reg = t.Get(row, "iso_region");
                if (reg is not null && regions.TryGetValue(reg, out var rId)) a.RegionId = rId; else missingRegion++;
            }
            await db.SaveChangesAsync(ct);
            progress?.Invoke(Math.Min(start + BatchSize, wanted.Count), wanted.Count);
        }

        var notes = new List<string>();
        if (missingCountry > 0) notes.Add($"{missingCountry:N0} airport(s) refer to a country that isn't loaded — import countries first.");
        if (missingRegion > 0) notes.Add($"{missingRegion:N0} airport(s) refer to a region that isn't loaded — import regions first.");
        logger.LogInformation("Airports import: {Added} added, {Updated} updated, {Skipped} skipped", added, updated, skipped);
        return new ImportResult(added, updated, skipped, notes);
    }

    // ------------------------------------------------------------------ ICAO manufacturers / aircraft types
    //
    // Unlike the OurAirports files these never overwrite: a code already stored is skipped, so
    // edits made here are kept. When a code appears more than once in the file, the first row wins.

    private static void RequireColumns(Csv.Table t, string what, params string[] columns)
    {
        var missing = columns.Where(c => !t.Has(c)).ToList();
        if (missing.Count > 0)
            throw new FormatException($"This doesn't look like an ICAO {what} file — missing column(s): {string.Join(", ", missing)}.");
    }

    /// <summary>icao_manufacturers.csv: Code, Name, State.</summary>
    public async Task<ImportResult> ImportManufacturersAsync(string csv, CancellationToken ct = default)
    {
        var t = new Csv.Table(csv);
        RequireColumns(t, "manufacturers", "Code", "Name");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var known = (await db.Manufacturers.Select(m => m.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int added = 0, existing = 0, duplicates = 0, invalid = 0;

        foreach (var row in t.Rows)
        {
            var code = Manufacturer.NormalizeCode(t.Get(row, "Code"));
            var name = t.Get(row, "Name");
            if (code is null || name is null || code.Length > 100) { invalid++; continue; }
            if (!seen.Add(code)) { duplicates++; continue; }        // first entry for a code wins
            if (known.Contains(code)) { existing++; continue; }    // never overwrite

            db.Manufacturers.Add(new Manufacturer { Code = code, Name = Cut(name, 200)!, State = Cut(t.Get(row, "State"), 100) });
            added++;
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Manufacturers import: {Added} added, {Existing} already stored, {Dup} duplicate codes, {Invalid} invalid", added, existing, duplicates, invalid);

        var notes = new List<string>();
        if (existing > 0) notes.Add($"{existing:N0} already stored — left as they are.");
        if (duplicates > 0) notes.Add($"{duplicates:N0} repeated code(s) in the file — only the first entry for each was used.");
        if (invalid > 0) notes.Add($"{invalid:N0} row(s) without a code or name were skipped.");
        return new ImportResult(added, 0, existing + duplicates + invalid, notes);
    }

    /// <summary>
    /// icao_aircraft_types.csv: Designator, Manufacturer (code), Model, Description, AircraftDescription,
    /// EngineCount, EngineType, WTC, WTG. Each type is linked to its manufacturer by code, so import
    /// manufacturers first.
    /// </summary>
    public async Task<ImportResult> ImportAircraftTypesAsync(string csv, CancellationToken ct = default)
    {
        var t = new Csv.Table(csv);
        RequireColumns(t, "aircraft types", "Designator", "Manufacturer", "Model");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var makers = await db.Manufacturers.AsNoTracking().ToDictionaryAsync(m => m.Code, m => m.Id, StringComparer.OrdinalIgnoreCase, ct);
        var known = (await db.IcaoAircraftTypes.Select(a => a.Designator).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missingMakers = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        int added = 0, existing = 0, duplicates = 0, invalid = 0;

        foreach (var row in t.Rows)
        {
            var designator = IcaoAircraftType.NormalizeDesignator(t.Get(row, "Designator"));
            var model = t.Get(row, "Model");
            if (designator is null || designator.Length > 4 || model is null) { invalid++; continue; }
            if (!seen.Add(designator)) { duplicates++; continue; }      // first entry for a designator wins
            if (known.Contains(designator)) { existing++; continue; }  // never overwrite

            var makerCode = Manufacturer.NormalizeCode(t.Get(row, "Manufacturer"));
            int? makerId = makerCode is not null && makers.TryGetValue(makerCode, out var mid) ? mid : null;
            if (makerCode is not null && makerId is null) missingMakers.Add(makerCode);

            db.IcaoAircraftTypes.Add(new IcaoAircraftType
            {
                Designator = designator,
                ManufacturerId = makerId,
                Model = Cut(model, 100)!,
                Description = Cut(t.Get(row, "Description"), 3),
                AircraftKind = Cut(t.Get(row, "AircraftDescription"), 20),
                EngineCount = Cut(t.Get(row, "EngineCount"), 2),
                EngineType = Cut(t.Get(row, "EngineType"), 30),
                Wtc = Cut(t.Get(row, "WTC"), 3),
                Wtg = Cut(t.Get(row, "WTG"), 1),
            });
            added++;
            if (added % BatchSize == 0) await db.SaveChangesAsync(ct);
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Aircraft types import: {Added} added, {Existing} already stored, {Dup} duplicate designators, {Invalid} invalid, {Missing} unknown manufacturers",
            added, existing, duplicates, invalid, missingMakers.Count);

        var notes = new List<string>();
        if (existing > 0) notes.Add($"{existing:N0} already stored — left as they are.");
        if (duplicates > 0) notes.Add($"{duplicates:N0} repeated designator(s) in the file — only the first entry for each was used.");
        if (invalid > 0) notes.Add($"{invalid:N0} row(s) without a designator or model were skipped.");
        if (missingMakers.Count > 0)
            notes.Add($"No manufacturer stored for {missingMakers.Count:N0} code(s), so those types aren't linked to one (import manufacturers first): " +
                      string.Join(", ", missingMakers.Take(15)) + (missingMakers.Count > 15 ? " …" : ""));
        return new ImportResult(added, 0, existing + duplicates + invalid, notes);
    }
}
