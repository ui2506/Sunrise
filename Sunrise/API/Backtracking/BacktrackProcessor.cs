using LabApi.Features.Wrappers;
using System;

namespace Sunrise.API.Backtracking;

/// <summary>
///     A disposable struct that temporarily resets player's position to a backtracked one, then restores it back.
/// </summary>
public readonly struct BacktrackProcessor : IDisposable
{
    readonly BacktrackEntry _previous;
    readonly Player _player;

    public BacktrackProcessor(Player player, BacktrackEntry claimed, bool forecast)
    {
        _player = player;
        _previous = new(_player);
        RestoreClosest(claimed, forecast);
    }

    void RestoreClosest(BacktrackEntry claimed, bool forecast)
    {
        BacktrackHistory history = BacktrackHistory.Get(_player);

        if (forecast)
            history.ForecastEntry();

        BacktrackEntry best = history.GetClosest(claimed);

        if (best.Timestamp != 0) // Null check struct edition
        {
            best.Restore(_player);

            if (Config.Instance.Debug)
            {
                Debug.Log($"Best entry found for {_player.Nickname} " +
                    $"Difference: A:{Quaternion.Angle(best.Rotation, claimed.Rotation):F5}, P:{Vector3.Distance(best.Position, claimed.Position):F} " +
                    $"Age: {best.Age * 1000:F0}ms");
            }
        }
        else
        {
            Debug.Log($"No suitable entry found for {_player.Nickname}");
        }
    }

    public void Dispose() // BUG: Prevents teleports inside OnShooting from working if player dies. is it possible by default tho?
    {
        _previous.Restore(_player);
    }
}