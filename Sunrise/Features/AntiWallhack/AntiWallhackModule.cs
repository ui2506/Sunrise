using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Extensions;
using LabApi.Features.Wrappers;
using NorthwoodLib.Pools;
using PlayerRoles;
using Sunrise.Features.AntiWallHack.PickupAntiWallHack;
using Sunrise.Features.AntiWallHack.PlayerAntiWallHack;

namespace Sunrise.Features.AntiWallHack;

internal class AntiWallHackModule : PluginModule
{
    static readonly Dictionary<Player, Vector3> DeathPositions = [];

    protected override List<PluginModule> SubModules { get; } =
    [
        new PlayerAntiWallHackModule(),
        new PickupAntiWallHackModule(),
    ];

    internal static bool TryGetDeathPosition(Player player, out Vector3 position) => DeathPositions.TryGetValue(player, out position);

    protected override void OnEnabled()
    {
        if (!Config.Instance.SpectatorAntiWallhack || (!Config.Instance.AntiWallhack && !Config.Instance.PickupAntiWallhack))
            return;

        Handlers.PlayerEvents.Left += OnLeft;
        Handlers.PlayerEvents.ChangingRole += OnChangingRole;
        Handlers.PlayerEvents.ChangedRole += OnChangedRole;
        Handlers.PlayerEvents.ChangedSpectator += OnChangedSpectator;

        base.OnEnabled();
    }

    protected override void OnDisabled()
    {
        Handlers.PlayerEvents.Left -= OnLeft;
        Handlers.PlayerEvents.ChangingRole -= OnChangingRole;
        Handlers.PlayerEvents.ChangedRole -= OnChangedRole;
        Handlers.PlayerEvents.ChangedSpectator -= OnChangedSpectator;

        base.OnDisabled();
    }

    protected override void OnReset() => DeathPositions.Clear();

    static void OnLeft(PlayerLeftEventArgs ev) => DeathPositions.Remove(ev.Player);

    static void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        if (ev.NewRole.RoleTypeId.IsAlive())
            DeathPositions.Remove(ev.Player);
    }

    static void OnChangingRole(PlayerChangingRoleEventArgs ev)
    {
        if (!ev.IsAllowed || ev.OldRole.RoleTypeId.IsDead() || ev.NewRole.IsAlive())
            return;

        Vector3 position = ev.Player.Position;
        DeathPositions[ev.Player] = position;

        List<Player> spectators = ev.Player.CurrentSpectators;

        foreach (Player spectator in spectators)
            DeathPositions[spectator] = position;

        ListPool<Player>.Shared.Return(spectators);
    }

    static void OnChangedSpectator(PlayerChangedSpectatorEventArgs ev)
    {
        if (Config.Instance.SpectatorAntiWallhack && ev.Player.Role == RoleTypeId.Spectator && ev.NewTarget != null && ev.NewTarget != ev.OldTarget)
            DeathPositions.Remove(ev.Player);
    }
}
