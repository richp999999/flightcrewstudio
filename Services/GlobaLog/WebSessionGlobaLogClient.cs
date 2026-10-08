using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;

namespace FlightCrewStudio.Services.GlobaLog;

/// <summary>
/// Authenticates with GlobaLog the way a browser does (Devise login form + session cookie) and
/// reads the crew member's logbook from the website's own CSV export
/// (<c>/flights/export?format=csv</c>).
///
/// GlobaLog's /api/v1 rejects web sessions (it only accepts an operator X-Token), but the CSV
/// export is available to any logged-in crew member and contains the complete logbook.
///
/// The CSV has no GlobaLog flight id, so the Flights list pages (/flights?limit=200&amp;page=N) are
/// read as well to map each CSV row to its real GlobaLog id by (off-block time, callsign). Crew
/// names come from each flight's own page (/flights/{id}), which the CSV does not include.
/// If a CSV row can't be matched to the list, it gets a deterministic synthetic id and no crew.
/// </summary>
public sealed partial class WebSessionGlobaLogClient : IGlobaLogClient
{
    private const string ExportPath = "/flights/export?format=csv";
    private static readonly string[] DateTimeFormats = ["dd/MM/yyyy HH:mm", "dd/MM/yyyy H:mm"];

    private readonly string _username;
    private readonly string _password;
    private readonly HttpClientHandler _handler;
    private readonly HttpClient _http;
    private bool _loggedIn;
    private ParsedLogbook? _logbook; // cached for the lifetime of this client (one sync run)
    private readonly HashSet<long> _realIds = new(); // flights whose id came from GlobaLog (crew page exists)
    private static readonly HtmlParser Html = new();

    /// <summary>Pause between flight-page requests, to be gentle with GlobaLog's website.</summary>
    private static readonly TimeSpan PageDelay = TimeSpan.FromMilliseconds(150);

    [GeneratedRegex("name=\"authenticity_token\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex AuthTokenRegex();

    public WebSessionGlobaLogClient(string baseUrl, string username, string password, int timeoutSeconds)
    {
        _username = username;
        _password = password;
        _handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AllowAutoRedirect = false // Devise returns 302 on success; we want to see it
        };
        _http = new HttpClient(_handler)
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("FlightCrewStudio/1.0");
    }

    // ---------------------------------------------------------------- login

    private async Task EnsureLoggedInAsync(CancellationToken ct)
    {
        if (_loggedIn) return;

        // 1. GET the sign-in page to obtain the CSRF token and the initial session cookie.
        string loginHtml;
        using (var pageResp = await _http.GetAsync("/users/sign_in", ct))
        {
            pageResp.EnsureSuccessStatusCode();
            loginHtml = await pageResp.Content.ReadAsStringAsync(ct);
        }

        var match = AuthTokenRegex().Match(loginHtml);
        if (!match.Success)
            throw new GlobaLogException("Could not find the login form's authenticity token — GlobaLog's sign-in page may have changed.");
        var authenticityToken = WebUtility.HtmlDecode(match.Groups[1].Value);

        // 2. POST credentials.
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["authenticity_token"] = authenticityToken,
            ["user[username]"] = _username,
            ["user[password]"] = _password,
            ["commit"] = "Sign In"
        });

        using (var loginResp = await _http.PostAsync("/users/sign_in", form, ct))
        {
            var redirected = loginResp.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther;
            var location = loginResp.Headers.Location?.ToString() ?? "";
            if (!redirected || location.Contains("sign_in", StringComparison.OrdinalIgnoreCase))
                throw new GlobaLogException("GlobaLog rejected the username/password.");
        }

        // 3. Confirm the session is really signed in: the dashboard must load, not bounce to sign-in.
        using (var dash = await _http.GetAsync("/dashboard", ct))
        {
            if (dash.StatusCode != HttpStatusCode.OK)
                throw new GlobaLogException("GlobaLog accepted the login form but did not open a signed-in session.");
        }

        _loggedIn = true;
    }

    // ---------------------------------------------------------------- IGlobaLogClient

    public async Task<GlobaLogHello> TestConnectionAsync(CancellationToken ct = default)
    {
        var book = await GetLogbookAsync(ct);
        return new GlobaLogHello("Logbook export OK", book.CrewName, book.Customer);
    }

    /// <summary>
    /// The CSV export returns the whole logbook in one go, so it is served as a single page,
    /// ordered by off-block descending (as the sync service expects).
    /// </summary>
    public async Task<GlobaLogFlightPage> GetFlightsPageAsync(int page, CancellationToken ct = default)
    {
        var book = await GetLogbookAsync(ct);
        return new GlobaLogFlightPage
        {
            Flights = page == 1 ? book.Flights : new List<GlobaLogFlight>(),
            Meta = new GlobaLogMeta { CurrentPage = page, TotalPages = 1, TotalCount = book.Flights.Count }
        };
    }

    private async Task<ParsedLogbook> GetLogbookAsync(CancellationToken ct)
    {
        if (_logbook is not null) return _logbook;
        await EnsureLoggedInAsync(ct);

        using var resp = await _http.GetAsync(ExportPath, ct);
        if (resp.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.Unauthorized)
            throw new GlobaLogException("GlobaLog's session expired or was refused while downloading the logbook export.");
        if (!resp.IsSuccessStatusCode)
            throw new GlobaLogException($"GlobaLog's logbook export failed ({(int)resp.StatusCode} {resp.ReasonPhrase}).");

        var csv = await resp.Content.ReadAsStringAsync(ct);
        var book = ParseLogbook(csv);

        // Swap synthetic ids for GlobaLog's real ones using the Flights list pages.
        var index = await GetFlightIndexAsync(book.Flights.Count, ct);
        foreach (var f in book.Flights)
        {
            if (index.TryGetValue(IndexKey(f.OffBlock, f.Name), out var realId))
            {
                f.Id = realId;
                _realIds.Add(realId);
            }
        }

        _logbook = book;
        return _logbook;
    }

    /// <summary>
    /// Read the Flights list pages and return (off-block|callsign) → GlobaLog flight id.
    /// Stops once it has as many entries as the CSV has flights, or the pages run out.
    /// </summary>
    private async Task<Dictionary<string, long>> GetFlightIndexAsync(int expected, CancellationToken ct)
    {
        var map = new Dictionary<string, long>();
        var range = Uri.EscapeDataString($"2000-01-01 - {DateTime.UtcNow.AddYears(1):yyyy-MM-dd}");

        for (var page = 1; page <= 100 && map.Count < expected; page++)
        {
            var html = await GetPageHtmlAsync($"/flights?date_range={range}&limit=200&page={page}", ct);
            var entries = ParseFlightIndex(html);
            if (entries.Count == 0) break;
            foreach (var (key, id) in entries) map.TryAdd(key, id);
        }
        return map;
    }

    internal static List<(string Key, long Id)> ParseFlightIndex(string html)
    {
        var doc = Html.ParseDocument(html);
        var result = new List<(string, long)>();
        foreach (var head in doc.QuerySelectorAll("[id^='flight_head_']"))
        {
            if (!long.TryParse(head.Id!["flight_head_".Length..], out var id)) continue;
            var cells = head.Children.Where(c => c.LocalName == "div").ToList();
            var callSign = cells.FirstOrDefault()?.TextContent.Trim();
            // First tooltip carrying a full timestamp is the off-block time ("2026-10-04 18:10:00").
            var offBlockText = head.QuerySelectorAll("[title]")
                .Select(e => e.GetAttribute("title")!)
                .FirstOrDefault(t => DateTime.TryParseExact(t, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
            if (callSign is null || offBlockText is null) continue;
            var offBlock = DateTime.ParseExact(offBlockText, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            result.Add((IndexKey(offBlock, callSign), id));
        }
        return result;
    }

    private static string IndexKey(DateTime? offBlock, string? callSign) =>
        $"{offBlock?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}|{callSign?.Trim().ToUpperInvariant()}";

    // ---------------------------------------------------------------- crew

    public async Task<GlobaLogFlightDetails?> GetFlightDetailsAsync(GlobaLogFlight flight, CancellationToken ct = default)
    {
        if (!_realIds.Contains(flight.Id)) return null; // no GlobaLog page known for this flight
        await Task.Delay(PageDelay, ct);
        var html = await GetPageHtmlAsync($"/flights/{flight.Id}", ct);
        var crew = ParseCrew(html);
        if (crew is null) return null;
        var (dep, arr) = ParseAirports(html);
        return new GlobaLogFlightDetails { Crew = crew, Departure = dep, Arrival = arr };
    }

    [GeneratedRegex(@"^(?:(?<iata>[A-Z0-9]{3})/)?(?<icao>[A-Z0-9]{4})$")]
    private static partial Regex AirportCodesRegex();
    [GeneratedRegex(@"^(?<name>.*?)\s*\((?<country>[^()]*)\)\s*$")]
    private static partial Regex PlaceRegex();

    /// <summary>
    /// Origin/Destination on a flight page look like:
    /// &lt;dt&gt;Origin&lt;/dt&gt;&lt;dd&gt;KLX/LGKL&lt;br&gt;Kalamata (Greece) &lt;a…/&gt;&lt;/dd&gt;
    /// </summary>
    internal static (GlobaLogAirport? Dep, GlobaLogAirport? Arr) ParseAirports(string html)
    {
        var doc = Html.ParseDocument(html);
        GlobaLogAirport? Read(string label)
        {
            var dt = doc.QuerySelectorAll("dt").FirstOrDefault(d => d.TextContent.Trim().Equals(label, StringComparison.OrdinalIgnoreCase));
            var dd = dt?.NextElementSibling;
            if (dd is null) return null;
            var lines = dd.ChildNodes.Where(n => n.NodeType == AngleSharp.Dom.NodeType.Text)
                .Select(n => n.TextContent.Trim()).Where(t => t.Length > 0).ToList();
            if (lines.Count == 0) return null;
            var codes = AirportCodesRegex().Match(lines[0]);
            if (!codes.Success) return null;
            var airport = new GlobaLogAirport
            {
                Icao = codes.Groups["icao"].Value,
                Iata = codes.Groups["iata"].Success ? codes.Groups["iata"].Value : null
            };
            if (lines.Count > 1)
            {
                var place = PlaceRegex().Match(lines[1]);
                airport.Name = place.Success ? place.Groups["name"].Value : lines[1];
                airport.Country = place.Success ? place.Groups["country"].Value : null;
            }
            return airport;
        }
        return (Read("Origin"), Read("Destination"));
    }

    /// <summary>
    /// Parse the "Crew" definition list on a flight page:
    /// &lt;h5&gt;Crew&lt;/h5&gt;&lt;dl&gt;&lt;dt&gt;Captain&lt;/dt&gt;&lt;dd&gt;Name &lt;a/&gt;&lt;br&gt;&lt;/dd&gt;&lt;dt&gt;Cabin crew&lt;/dt&gt;&lt;dd&gt;A&lt;br&gt;B&lt;br&gt;&lt;/dd&gt;…
    /// Each text line in a &lt;dd&gt; is one person; it may be a staff number instead of a name.
    /// Returns null if the page has no Crew section (layout changed).
    /// </summary>
    internal static List<GlobaLogCrew>? ParseCrew(string html)
    {
        var doc = Html.ParseDocument(html);
        var heading = doc.QuerySelectorAll("h5").FirstOrDefault(h => h.TextContent.Trim().Equals("Crew", StringComparison.OrdinalIgnoreCase));
        var dl = heading?.NextElementSibling;
        if (dl is null || dl.LocalName != "dl") return null;

        var crew = new List<GlobaLogCrew>();
        string? role = null;
        foreach (var el in dl.Children)
        {
            if (el.LocalName == "dt") { role = el.TextContent.Trim(); continue; }
            if (el.LocalName != "dd" || role is null) continue;

            foreach (var node in el.ChildNodes.Where(n => n.NodeType == AngleSharp.Dom.NodeType.Text))
            {
                var text = node.TextContent.Trim();
                if (text.Length == 0) continue;
                crew.Add(CrewMemberLooksLikeNumber(text)
                    ? new GlobaLogCrew { LoadId = text, Role = role }
                    : new GlobaLogCrew { Name = text, Role = role });
            }
        }
        return crew;
    }

    private static bool CrewMemberLooksLikeNumber(string s) => s.Length >= 4 && s.All(char.IsAsciiDigit);

    /// <summary>GET an HTML page, logging in again once if the session has expired.</summary>
    private async Task<string> GetPageHtmlAsync(string path, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            await EnsureLoggedInAsync(ct);
            using var resp = await _http.GetAsync(path, ct);
            if (resp.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.Unauthorized)
            {
                if (attempt == 0) { _loggedIn = false; continue; }
                throw new GlobaLogException($"GlobaLog's session was refused while loading {path}.");
            }
            if (!resp.IsSuccessStatusCode)
                throw new GlobaLogException($"GlobaLog returned {(int)resp.StatusCode} {resp.ReasonPhrase} for {path}.");
            return await resp.Content.ReadAsStringAsync(ct);
        }
    }

    // ---------------------------------------------------------------- CSV parsing

    internal sealed record ParsedLogbook(string? CrewName, string? Customer, List<GlobaLogFlight> Flights);

    /// <summary>
    /// Layout of GlobaLog's export:
    ///   "load_id","name" / "40008658","Richard PALMER" / blank /
    ///   header row starting "date_of_flight" / one row per flight / "---" / footer lines
    ///   (including "Customer: &lt;operator&gt;") / "END_OF_REPORT."
    /// Columns are located by header name, so a re-ordered export still parses.
    /// </summary>
    internal static ParsedLogbook ParseLogbook(string csv)
    {
        var rows = ParseCsv(csv);

        string? crewName = null, customer = null;
        if (rows.Count > 1 && rows[0].Count >= 2 && rows[0][0] == "load_id")
        {
            crewName = NullIfEmpty(rows[1].ElementAtOrDefault(1));
        }

        var headerIdx = rows.FindIndex(r => r.Count > 1 && r[0] == "date_of_flight");
        if (headerIdx < 0)
            throw new GlobaLogException("GlobaLog's logbook export did not contain the expected flight table — the export format may have changed.");

        var header = rows[headerIdx];
        var col = header.Select((name, i) => (name, i)).ToDictionary(x => x.name, x => x.i, StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "call_sign", "departure_airport", "off_block", "destination_airport", "on_block" })
            if (!col.ContainsKey(required))
                throw new GlobaLogException($"GlobaLog's logbook export is missing the '{required}' column — the export format may have changed.");

        string? Get(List<string> r, string name) =>
            col.TryGetValue(name, out var i) && i < r.Count ? NullIfEmpty(r[i]) : null;

        var flights = new List<GlobaLogFlight>();
        var i = headerIdx + 1;
        for (; i < rows.Count; i++)
        {
            var r = rows[i];
            if (r.Count == 1 && r[0] == "---") break;               // end of flight table
            if (r.Count < header.Count / 2) continue;               // blank / stray line

            var callSign = Get(r, "call_sign");
            var dep = Get(r, "departure_airport");
            var offBlock = ParseDateTime(Get(r, "off_block"));

            var f = new GlobaLogFlight
            {
                Name = callSign,
                OffBlock = offBlock,
                DepartureTime = ParseDateTime(Get(r, "take_off")),
                ArrivalTime = ParseDateTime(Get(r, "landing")),
                OnBlock = ParseDateTime(Get(r, "on_block")),
                RadiationAmount = ParseDouble(Get(r, "cosmic_radiation")),
                TotalDayMinutes = ParseMinutes(Get(r, "day_time")),
                TotalNightMinutes = ParseMinutes(Get(r, "night_time")),
                TotalInstrMinutes = ParseMinutes(Get(r, "instrument_time")),
                NightLanding = string.Equals(Get(r, "night_landing"), "night", StringComparison.OrdinalIgnoreCase),
                Departure = new GlobaLogEndpoint { Airport = new GlobaLogAirport { Icao = dep } },
                Arrival = new GlobaLogEndpoint { Airport = new GlobaLogAirport { Icao = Get(r, "destination_airport") } },
                MyRole = Get(r, "role_on_flight"),
                PilotFlying = Get(r, "flying_pilot") switch { "Yes" => true, "No" => false, _ => null },
                Aircraft = new GlobaLogAircraft
                {
                    Icao = Get(r, "aircraft_model"),              // e.g. "A-321" (model, not a strict ICAO type code)
                    Registration = Get(r, "aircraft_registration"),
                    Manufacturer = Get(r, "aircraft_manufacturer")
                }
            };

            f.Id = SyntheticId(offBlock, callSign, dep);
            flights.Add(f);
        }

        for (; i < rows.Count; i++)
        {
            var line = rows[i].ElementAtOrDefault(0) ?? "";
            if (line.StartsWith("Customer:", StringComparison.OrdinalIgnoreCase))
                customer = NullIfEmpty(line["Customer:".Length..].Trim());
        }

        flights = flights.OrderByDescending(f => f.OffBlock ?? DateTime.MinValue).ToList();
        return new ParsedLogbook(crewName, customer, flights);
    }

    /// <summary>Stable positive 63-bit id from the flight's natural key.</summary>
    internal static long SyntheticId(DateTime? offBlock, string? callSign, string? dep)
    {
        var key = $"{offBlock?.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)}|{callSign?.ToUpperInvariant()}|{dep?.ToUpperInvariant()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return BitConverter.ToInt64(hash, 0) & long.MaxValue;
    }

    /// <summary>GlobaLog logbook times are UTC.</summary>
    private static DateTime? ParseDateTime(string? s) =>
        s is not null && DateTime.TryParseExact(s, DateTimeFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d)
            ? DateTime.SpecifyKind(d, DateTimeKind.Utc)
            : null;

    private static double? ParseDouble(string? s) =>
        s is not null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>"h:mm" → minutes.</summary>
    private static int ParseMinutes(string? s)
    {
        if (s is null) return 0;
        var parts = s.Split(':');
        return parts.Length == 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m) ? h * 60 + m : 0;
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    internal static List<List<string>> ParseCsv(string text) => FlightCrewStudio.Services.Csv.Parse(text);

    public void Dispose()
    {
        _http.Dispose();
        _handler.Dispose();
    }
}
