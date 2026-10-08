namespace FlightCrewStudio.Data;

/// <summary>How the app authenticates to GlobaLog when fetching flights.</summary>
public enum GlobaLogAuthMode
{
    /// <summary>Log in via GlobaLog's web form with username/password, reuse the session cookie.</summary>
    WebSession = 0,

    /// <summary>Use an operator-issued API token sent in the X-Token header.</summary>
    ApiToken = 1
}
