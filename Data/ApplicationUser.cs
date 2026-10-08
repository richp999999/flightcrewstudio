using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace FlightCrewStudio.Data;

// Application user. DisplayName is the editable "account name" shown in the UI.
public class ApplicationUser : IdentityUser
{
    [PersonalData]
    public string? DisplayName { get; set; }

    /// <summary>Standard (default for new users) or Administrator.</summary>
    public UserType UserType { get; set; } = UserType.Standard;

    /// <summary>Name printed on logbook exports (e.g. "Richard Palmer").</summary>
    [PersonalData, MaxLength(100)]
    public string? LogbookName { get; set; }

    /// <summary>Pilot licence number printed on logbook exports.</summary>
    [PersonalData, MaxLength(50)]
    public string? LicenceNumber { get; set; }
}
