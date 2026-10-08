namespace FlightCrewStudio.Services.GlobaLog;

public class GlobaLogOptions
{
    public const string SectionName = "GlobaLog";

    /// <summary>Base URL of the GlobaLog instance.</summary>
    public string BaseUrl { get; set; } = "https://next.app.globalog.com";

    /// <summary>Per-request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 60;
}
