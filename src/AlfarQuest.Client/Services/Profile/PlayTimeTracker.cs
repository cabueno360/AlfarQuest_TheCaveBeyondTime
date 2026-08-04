using System.Diagnostics;
using AlfarQuest.Shared.Profiles;

namespace AlfarQuest.Client.Services.Profile;

/// <summary>Turns a play session into the four numbers the profile shows.
///
/// It exists so the game page does not have to know what a PlayerStatsDto is, and
/// so the rule "only report what was actually played" lives in one testable place
/// rather than inside a Blazor lifecycle method.
///
/// Reports are <em>deltas</em>, not totals, because the server accumulates play
/// time. Sending the running total each checkpoint would count the same minutes
/// again and again.</summary>
public sealed class PlayTimeTracker(PartyState party, ProfileService profiles)
{
    /// <summary>How often a long session checkpoints. Leaving the page reports the
    /// remainder — but a closed tab or a crash never gets to, so without this a
    /// three-hour delve that ended in a browser restart would count for nothing.
    /// Two minutes is the most that can be lost.</summary>
    private static readonly TimeSpan Checkpoint = TimeSpan.FromMinutes(2);

    /// <summary>Below this, a report is a page that was opened and closed rather
    /// than a session, and would only add noise to the profile.</summary>
    private const long MinimumSeconds = 10;

    private readonly Stopwatch _elapsed = new();
    private long _reported;
    private CancellationTokenSource? _checkpoints;

    /// <summary>How long this session has run — what the Delve tab shows. Reading
    /// it does not disturb the reporting maths, which works in deltas.</summary>
    public TimeSpan Elapsed => _elapsed.Elapsed;

    public void Start()
    {
        _reported = 0;
        _elapsed.Restart();

        _checkpoints?.Cancel();
        _checkpoints = new CancellationTokenSource();
        _ = CheckpointLoopAsync(_checkpoints.Token);
    }

    /// <summary>Stops the clock and sends whatever has not been reported yet.
    /// Called when the player leaves the game. Safe to call twice — the second
    /// call finds nothing left to send.</summary>
    public async Task ReportAsync()
    {
        _checkpoints?.Cancel();
        _checkpoints = null;
        _elapsed.Stop();
        await FlushAsync();
    }

    private async Task CheckpointLoopAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(Checkpoint);
            while (await timer.WaitForNextTickAsync(ct)) await FlushAsync();
        }
        catch (OperationCanceledException)
        {
            // The player left. ReportAsync is sending the remainder.
        }
    }

    private async Task FlushAsync()
    {
        var elapsed = (long)_elapsed.Elapsed.TotalSeconds;
        var unreported = elapsed - _reported;
        if (unreported < MinimumSeconds) return;

        // Claimed before the call, not after: a slow request must not let the next
        // checkpoint send the same seconds a second time.
        _reported = elapsed;

        await profiles.ReportStatsAsync(new PlayerStatsDto
        {
            CurrentCharacter = party.Selected?.Name,
            HighestLevel = party.Members.Count == 0 ? 0 : party.Members.Max(m => m.Level),
            // Gold is per hero now; the profile's total is the party's purse across
            // all of them (or the shared pool, if that mode is on).
            TotalGold = party.SharedGold ? party.SharedPurse["gold"] : party.Members.Sum(m => m.Purse["gold"]),
            TotalPlayTimeSeconds = unreported,
        });
    }
}
