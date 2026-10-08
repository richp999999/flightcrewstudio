using System.Globalization;
using FlightCrewStudio.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Services.Admin;

/// <summary>Outcome of a CSV import, shown on the admin page.</summary>
public sealed record ImportResult(int Added, int Updated, int Skipped, IReadOnlyList<string> Notes)
{
    public string Summary => $"{Added:N0} added, {Updated:N0} updated" + (Skipped > 0 ? $", {Skipped:N0} skipped" : "") + ".";
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
}
