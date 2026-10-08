using FlightCrewStudio.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Services;

/// <summary>
/// Resolves ICAO codes to the shared <see cref="Airport"/> list during a sync, adding new airports
/// and filling in IATA / name / town / country only where they're empty (edits and imports win).
/// GlobaLog country names (e.g. "United Kingdom") are matched to <see cref="Country"/> by name.
/// </summary>
public sealed class AirportLookup
{
    private readonly ApplicationDbContext _db;
    private readonly Dictionary<string, Airport> _byIcao;
    private readonly Dictionary<string, int> _countryIdByName;

    private AirportLookup(ApplicationDbContext db, IEnumerable<Airport> airports, Dictionary<string, int> countries)
    {
        _db = db;
        _byIcao = airports.ToDictionary(a => a.Icao, StringComparer.OrdinalIgnoreCase);
        _countryIdByName = countries;
    }

    public static async Task<AirportLookup> LoadAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var countries = (await db.Countries.AsNoTracking().Select(c => new { c.Name, c.Id }).ToListAsync(ct))
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        // Only the airports flights actually use are needed in memory; others are found on demand.
        return new(db, await db.Airports.Where(a => db.Flights.Any(f => f.DepartureAirportId == a.Id || f.ArrivalAirportId == a.Id)).ToListAsync(ct), countries);
    }

    public Airport? Resolve(string? icao, string? iata = null, string? name = null, string? country = null)
    {
        var code = Airport.NormalizeIcao(icao);
        if (code is null || code.Length > 4) return null;

        if (!_byIcao.TryGetValue(code, out var airport))
        {
            airport = _db.Airports.FirstOrDefault(a => a.Icao == code);   // e.g. loaded by an import
            if (airport is null)
            {
                airport = new Airport { Icao = code };
                _db.Airports.Add(airport);
            }
            _byIcao[code] = airport;
        }
        if (airport.Iata is null && !string.IsNullOrWhiteSpace(iata) && iata.Trim().Length == 3) airport.Iata = iata.Trim().ToUpperInvariant();
        // GlobaLog's airport "name" is really the town (e.g. "Kalamata").
        if (!string.IsNullOrWhiteSpace(name))
        {
            airport.Municipality ??= name.Trim();
            airport.Name ??= name.Trim();
        }
        if (airport.CountryId is null && !string.IsNullOrWhiteSpace(country) && _countryIdByName.TryGetValue(country.Trim(), out var cId))
            airport.CountryId = cId;
        return airport;
    }
}
