using System.ComponentModel.DataAnnotations;

namespace FlightCrewStudio.Data;

/// <summary>
/// A user's GlobaLog credentials. All secret fields are stored as ciphertext only —
/// they are encrypted by <see cref="Services.ICredentialProtector"/> using a key held on
/// the server, so the database never contains plaintext credentials.
/// </summary>
public class GlobaLogCredential
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = default!;
    public ApplicationUser? User { get; set; }

    public GlobaLogAuthMode AuthMode { get; set; } = GlobaLogAuthMode.WebSession;

    /// <summary>Encrypted GlobaLog username (web-session mode).</summary>
    public string? UsernameCipher { get; set; }

    /// <summary>Encrypted GlobaLog password (web-session mode).</summary>
    public string? PasswordCipher { get; set; }

    /// <summary>Encrypted GlobaLog API token (X-Token mode).</summary>
    public string? ApiTokenCipher { get; set; }

    /// <summary>
    /// The crew member record that is the user themself, as listed on GlobaLog flight pages.
    /// Used to fill <see cref="Flight.MyCrewRoleId"/> from each flight's crew list.
    /// </summary>
    public int? MyCrewMemberId { get; set; }
    public CrewMember? MyCrewMember { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
