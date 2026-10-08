using System.Collections.Concurrent;

namespace FlightCrewStudio.Services.Sync;

public enum SyncPhase { Idle, Connecting, Fetching, Saving, Completed, Failed }

/// <summary>Live, mutable status of one user's sync run. Observed by the Sync page.</summary>
public sealed class SyncProgress
{
    public SyncPhase Phase { get; set; } = SyncPhase.Idle;
    public bool IsRunning => Phase is SyncPhase.Connecting or SyncPhase.Fetching or SyncPhase.Saving;

    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int FlightsSeen { get; set; }
    public int NewFlights { get; set; }
    public int UpdatedFlights { get; set; }

    public string? Message { get; set; }
    public string? Error { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    /// <summary>Raised whenever a field changes, so the UI can re-render.</summary>
    public event Action? Changed;
    public void NotifyChanged() => Changed?.Invoke();
}

/// <summary>
/// Holds the current <see cref="SyncProgress"/> per user. Registered as a singleton so a sync
/// started on a background task survives even if the user navigates away, and so the Sync page
/// can pick the run back up when they return.
/// </summary>
public sealed class SyncProgressTracker
{
    private readonly ConcurrentDictionary<string, SyncProgress> _byUser = new();

    public SyncProgress GetOrCreate(string userId) => _byUser.GetOrAdd(userId, _ => new SyncProgress());

    public bool IsRunning(string userId) => _byUser.TryGetValue(userId, out var p) && p.IsRunning;
}
