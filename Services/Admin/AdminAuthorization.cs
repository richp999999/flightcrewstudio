using System.Security.Claims;
using FlightCrewStudio.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlightCrewStudio.Services.Admin;

/// <summary>Requirement behind the "Admin" policy.</summary>
public sealed class AdministratorRequirement : IAuthorizationRequirement;

/// <summary>
/// Grants the "Admin" policy when the signed-in user is an administrator. Checked against the
/// database on every evaluation, so a change of user type takes effect without signing out.
/// A user whose email is in <see cref="AdminOptions.AdministratorEmails"/> is always an
/// administrator; their stored type is corrected to Administrator if it isn't already.
/// </summary>
public sealed class AdministratorHandler(IDbContextFactory<ApplicationDbContext> dbFactory, IOptions<AdminOptions> options)
    : AuthorizationHandler<AdministratorRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdministratorRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return;

        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return;

        if (options.Value.IsAlwaysAdministrator(user.Email))
        {
            if (user.UserType != UserType.Administrator)
            {
                user.UserType = UserType.Administrator;
                await db.SaveChangesAsync();
            }
            context.Succeed(requirement);
        }
        else if (user.UserType == UserType.Administrator)
        {
            context.Succeed(requirement);
        }
    }

    /// <summary>At startup: make sure every always-administrator account is stored as Administrator.</summary>
    public static async Task EnsureAdministratorsAsync(ApplicationDbContext db, AdminOptions options, ILogger logger)
    {
        var emails = options.AdministratorEmails.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim().ToUpperInvariant()).ToList();
        if (emails.Count == 0) return;
        var users = await db.Users.Where(u => u.NormalizedEmail != null && emails.Contains(u.NormalizedEmail) && u.UserType != UserType.Administrator).ToListAsync();
        foreach (var u in users)
        {
            u.UserType = UserType.Administrator;
            logger.LogInformation("Set {Email} to Administrator.", u.Email);
        }
        if (users.Count > 0) await db.SaveChangesAsync();
    }
}
