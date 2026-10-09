using System.Globalization;
using FlightCrewStudio.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Services;

/// <summary>
/// The name and licence number printed on logbook exports, set in Settings → Me. When no name has
/// been entered, the account's display name is used, then "me" from the crew lists
/// ("Richard PALMER" → "Richard Palmer"), then the user name.
/// </summary>
public static class PilotIdentity
{
    public static async Task<(string Name, string? Licence)> GetAsync(ApplicationDbContext db, string userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        return (await DefaultNameAsync(db, user, ct), user.LicenceNumber);
    }

    public static async Task<string> DefaultNameAsync(ApplicationDbContext db, ApplicationUser user, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(user.LogbookName)) return user.LogbookName;
        if (!string.IsNullOrWhiteSpace(user.DisplayName)) return user.DisplayName;
        var me = await db.GlobaLogCredentials.AsNoTracking().Where(c => c.UserId == user.Id)
            .Select(c => c.MyCrewMember!.Name).FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(me)) return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(me.ToLowerInvariant());
        return user.UserName ?? "";
    }
}
