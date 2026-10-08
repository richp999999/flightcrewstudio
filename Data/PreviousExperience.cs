using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace FlightCrewStudio.Data;

/// <summary>
/// The user's opening balances — flying done before the flights stored in this app (Settings →
/// Previous Experience). Times are stored as whole minutes; take-offs, landings and approaches as
/// counts. They are added to the all-time totals wherever those are shown: the dashboard, the
/// Flights page's brought-forward line and the EASA PDF's "Total From Previous Pages".
/// One row per user; a missing row means all zeros.
/// </summary>
[Index(nameof(UserId), IsUnique = true)]
public class PreviousExperience
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = default!;
    public ApplicationUser? User { get; set; }

    public int TotalMinutes { get; set; }
    public int SingleEngineMinutes { get; set; }
    public int MultiEngineMinutes { get; set; }
    public int MultiPilotMinutes { get; set; }
    public int NightMinutes { get; set; }
    public int IfrMinutes { get; set; }
    public int PicMinutes { get; set; }
    public int CoPilotMinutes { get; set; }
    public int DualMinutes { get; set; }
    public int InstructorMinutes { get; set; }
    public int SimulatorMinutes { get; set; }

    public int TakeoffsDay { get; set; }
    public int TakeoffsNight { get; set; }
    public int LandingsDay { get; set; }
    public int LandingsNight { get; set; }
    public int InstrumentApproaches { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Day time isn't entered separately: it's the total less night.</summary>
    public int DayMinutes => Math.Max(0, TotalMinutes - NightMinutes);

    /// <summary>The user's previous experience, or an all-zero record if none has been entered.</summary>
    public static async Task<PreviousExperience> ForUserAsync(ApplicationDbContext db, string userId, CancellationToken ct = default) =>
        await db.PreviousExperience.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct)
        ?? new PreviousExperience { UserId = userId };
}
