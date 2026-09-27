using LabApi.Features.Wrappers;
using System;
using PlayerRoles.FirstPersonControl;

namespace Sunrise.API.Backtracking;

/// <summary>
///     A disposable struct that temporarily resets player's position to a backtracked one, then restores it back.
/// </summary>
public readonly struct BacktrackProcessor : IDisposable
{
    readonly FpcBacktracker? _backtracker;

    public BacktrackProcessor(Player player, BacktrackEntry claimed, bool forecast)
    {
        _backtracker = null;
        BacktrackHistory history = BacktrackHistory.Get(player);

        if (forecast)
            history.ForecastEntry();

        BacktrackEntry best = history.GetClosest(claimed);

        if (best.Timestamp != 0) // Null check struct edition
        {
            if (Config.Instance.Debug)
            {
                Debug.Log($"Best entry found for {player.Nickname} " +
                    $"Difference: A:{Quaternion.Angle(best.Rotation, claimed.Rotation):F5}, P:{Vector3.Distance(best.Position, claimed.Position):F} " +
                    $"Age: {best.Age * 1000:F0}ms");
            }

            _backtracker = new(player.ReferenceHub, best.Position, best.Rotation,
                Config.Instance.AccountedLatencySeconds, forecast ? Config.Instance.AccountedLatencySeconds : 0);
        }
        else
        {
            Debug.Log($"No suitable entry found for {player.Nickname}");
        }
    }

    public void Dispose() => _backtracker?.Dispose();
}
