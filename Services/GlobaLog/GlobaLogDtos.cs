using System.Text.Json.Serialization;

namespace FlightCrewStudio.Services.GlobaLog;

// DTOs mirroring GlobaLog's /api/v1 JSON. Only the fields we store are mapped.

public record GlobaLogHello(
    [property: JsonPropertyName("hello")] string? Hello,
    [property: JsonPropertyName("your-name")] string? YourName,
    [property: JsonPropertyName("operator")] string? Operator);

public class GlobaLogFlightPage
{
    [JsonPropertyName("flights")] public List<GlobaLogFlight> Flights { get; set; } = new();
    [JsonPropertyName("meta")] public GlobaLogMeta Meta { get; set; } = new();
}

public class GlobaLogMeta
{
    [JsonPropertyName("current_page")] public int CurrentPage { get; set; }
    [JsonPropertyName("total_pages")] public int TotalPages { get; set; }
    [JsonPropertyName("total_count")] public int TotalCount { get; set; }
}

public class GlobaLogFlight
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("departure_time")] public DateTime? DepartureTime { get; set; }
    [JsonPropertyName("off_block")] public DateTime? OffBlock { get; set; }
    [JsonPropertyName("arrival_time")] public DateTime? ArrivalTime { get; set; }
    [JsonPropertyName("on_block")] public DateTime? OnBlock { get; set; }
    [JsonPropertyName("radiation_amount")] public double? RadiationAmount { get; set; }
    [JsonPropertyName("total_day_minutes")] public int TotalDayMinutes { get; set; }
    [JsonPropertyName("total_night_minutes")] public int TotalNightMinutes { get; set; }
    [JsonPropertyName("total_instr_minutes")] public int TotalInstrMinutes { get; set; }
    [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTime? UpdatedAt { get; set; }
    [JsonPropertyName("night_landing")] public bool NightLanding { get; set; }
    [JsonPropertyName("foreign_id")] public string? ForeignId { get; set; }

    [JsonPropertyName("departure")] public GlobaLogEndpoint? Departure { get; set; }
    [JsonPropertyName("arrival")] public GlobaLogEndpoint? Arrival { get; set; }
    [JsonPropertyName("aircraft")] public GlobaLogAircraft? Aircraft { get; set; }
    [JsonPropertyName("crew")] public List<GlobaLogCrew> Crew { get; set; } = new();

    // Website (CSV export) only:
    [JsonIgnore] public string? MyRole { get; set; }
    [JsonIgnore] public bool? PilotFlying { get; set; }
}

/// <summary>Extra detail for one flight: its crew list and fuller airport information.</summary>
public class GlobaLogFlightDetails
{
    public List<GlobaLogCrew> Crew { get; set; } = new();
    public GlobaLogAirport? Departure { get; set; }
    public GlobaLogAirport? Arrival { get; set; }
}

public class GlobaLogEndpoint
{
    [JsonPropertyName("airport")] public GlobaLogAirport? Airport { get; set; }
}

public class GlobaLogAirport
{
    [JsonPropertyName("icao")] public string? Icao { get; set; }
    [JsonPropertyName("iata")] public string? Iata { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("country")] public string? Country { get; set; }
}

public class GlobaLogAircraft
{
    [JsonPropertyName("icao")] public string? Icao { get; set; }
    [JsonPropertyName("registration")] public string? Registration { get; set; }
    /// <summary>Website (CSV export) only.</summary>
    [JsonIgnore] public string? Manufacturer { get; set; }
}

public class GlobaLogCrew
{
    /// <summary>Staff number. The API returns this; the website sometimes shows it in place of a name.</summary>
    [JsonPropertyName("load_id")] public string? LoadId { get; set; }
    /// <summary>Crew member's name (website flight pages only; the API does not return names).</summary>
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("role")] public string? Role { get; set; }
}
