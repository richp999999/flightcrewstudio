namespace FlightCrewStudio.Services.Admin;

/// <summary>
/// "Admin" configuration section. Users whose email is listed in
/// <see cref="AdministratorEmails"/> are always administrators, whatever their stored user type.
/// </summary>
public class AdminOptions
{
    public const string SectionName = "Admin";
    public const string Policy = "Admin";

    public List<string> AdministratorEmails { get; set; } = ["richard.palmer@ripco.co.uk"];

    public bool IsAlwaysAdministrator(string? email) =>
        !string.IsNullOrWhiteSpace(email) &&
        AdministratorEmails.Any(e => string.Equals(e.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));
}
