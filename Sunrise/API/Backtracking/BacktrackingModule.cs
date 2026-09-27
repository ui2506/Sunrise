using LabApi.Events.Arguments.PlayerEvents;

namespace Sunrise.API.Backtracking;

/// <summary>
///     Serverside backtrack works by recording a precise history of player positions and rotations.
///     When a player shoots, the server finds the best values from the history instead of blindly trusting the client over their position and rotation values.
///     This prevents any cheats that include shooting in a different direction than the actual one.
/// </summary>
internal class BacktrackingModule : PluginModule
{
    protected override void OnEnabled()
    {
        Handlers.ServerEvents.RoundRestarted += OnReset;
        Handlers.PlayerEvents.Left += OnPlayerLeft;
        Handlers.PlayerEvents.ChangedRole += OnChangedRole;
    }

    protected override void OnDisabled()
    {
        Handlers.ServerEvents.RoundRestarted -= OnReset;
        Handlers.PlayerEvents.Left -= OnPlayerLeft;
        Handlers.PlayerEvents.ChangedRole -= OnChangedRole;
    }

    static void OnPlayerLeft(PlayerLeftEventArgs ev) => BacktrackHistory.Dictionary.Remove(ev.Player.ReferenceHub);
    static void OnChangedRole(PlayerChangedRoleEventArgs ev) => BacktrackHistory.Dictionary.Remove(ev.Player.ReferenceHub);

    protected override void OnReset()
    {
        BacktrackHistory.Dictionary.Clear();
    }
}
