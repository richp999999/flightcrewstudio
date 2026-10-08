using System.ComponentModel.DataAnnotations;

namespace FlightCrewStudio.Data;

/// <summary>
/// Per-user sync bookkeeping. <see cref="LastSyncedThrough"/> is the high-water mark:
/// flights with an off-block time at or before it are considered already fetched. The user
/// can edit it (e.g. move it back) to force a re-download of older flights.
/// </summary>
public class SyncState
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = default!;
    public ApplicationUser? User { get; set; }

    /// <summary>High-water mark (off-block time). Null means "never synced — fetch everything".</summary>
    public DateTime? LastSyncedThrough { get; set; }

    /// <summary>When the last successful sync run completed.</summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>Outcome message from the last run.</summary>
    public string? LastRunMessage { get; set; }
}
