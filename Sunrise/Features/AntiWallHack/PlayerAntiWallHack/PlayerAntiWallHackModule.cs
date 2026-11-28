using HarmonyLib;
using InventorySystem.Items;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.NetworkMessages;
using PlayerRoles.PlayableScps.Scp049;
using PlayerRoles.PlayableScps.Scp096;
using Sunrise.API.Visibility;
using Sunrise.Features.AntiWallHack.PlayerAntiWallHack.ForcedVisibility;

namespace Sunrise.Features.AntiWallHack.PlayerAntiWallHack;

internal class PlayerAntiWallHackModule : PluginModule
{
    // After player lands their visibility should stay on for some time
    internal static readonly Dictionary<Player, float> BypassedPlayers = [];

    static readonly Dictionary<Player, List<Player>> VisiblePlayers = [];
    static readonly Dictionary<Player, float> VisibilityRanges = [];
    static int rangeFrame = -1;

    static readonly AutoBenchmark SpectatorBenchmark = new("PlayerAntiWallHackModule.OnValidatedVisibility (spectator)");
    static readonly AutoBenchmark PlayerBenchmark = new("PlayerAntiWallHackModule.IsPlayerVisible (before raycast)");

    protected override void OnEnabled()
    {
        if (!Config.Instance.AntiWallhack)
        {
            Debug.Log("AntiWallHack module disabled according to the config!");
            return;
        }

        SunrisePlugin.Harmony.CreateClassProcessor(typeof(PlayerLandingPatch)).Patch();

        Handlers.PlayerEvents.ValidatedVisibility += OnValidatedVisibility;
        Handlers.PlayerEvents.ShootingWeapon += OnShootingWeapon;
        Handlers.PlayerEvents.Left += OnPlayerLeft;
        Handlers.PlayerEvents.ChangedRole += OnChangedRole;

        if (Config.Instance.SpectatorAntiWallhack)
            Handlers.PlayerEvents.ChangedSpectator += OnChangedSpectator;

        base.OnEnabled();
    }

    protected override void OnDisabled()
    {
        Handlers.PlayerEvents.ValidatedVisibility -= OnValidatedVisibility;
        Handlers.PlayerEvents.ShootingWeapon -= OnShootingWeapon;
        Handlers.PlayerEvents.Left -= OnPlayerLeft;
        Handlers.PlayerEvents.ChangedRole -= OnChangedRole;
        Handlers.PlayerEvents.ChangedSpectator -= OnChangedSpectator;

        base.OnDisabled();
    }

    protected override void OnReset()
    {
        BypassedPlayers.Clear();
        VisiblePlayers.Clear();
        VisibilityRanges.Clear();
        RaycastVisibilityCache.Clear();
    }

    static void OnShootingWeapon(PlayerShootingWeaponEventArgs ev) => BypassedPlayers[ev.Player] = Time.time + 1f;

    static void OnPlayerLeft(PlayerLeftEventArgs ev)
    {
        BypassedPlayers.Remove(ev.Player);
        RemoveVisibility(ev.Player);
        RaycastVisibilityCache.Remove(ev.Player.PlayerId);
    }

    static void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        BypassedPlayers.Remove(ev.Player);
        RemoveVisibility(ev.Player);
        RaycastVisibilityCache.Remove(ev.Player.PlayerId);
    }

    static void OnChangedSpectator(PlayerChangedSpectatorEventArgs ev)
    {
        if (!Config.Instance.SpectatorAntiWallhack || ev.Player.Role != RoleTypeId.Spectator || ev.NewTarget == ev.OldTarget)
            return;

        ev.Player.Connection.Send(new FpcPositionMessage(ev.Player.ReferenceHub));
    }

    static void OnValidatedVisibility(PlayerValidatedVisibilityEventArgs ev)
    {
        if (ev.Player == ev.Target)
            return;

        if (Config.Instance.SpectatorAntiWallhack && ev.Player.Role == RoleTypeId.Spectator)
        {
            using var measurement = SpectatorBenchmark.Start();

            if (!ev.IsVisible)
                return;

            if (AntiWallHackModule.TryGetDeathPosition(ev.Player, out Vector3 position))
            {
                if ((position - ev.Target.Position).sqrMagnitude > 30f * 30f)
                    ev.IsVisible = false;

                return;
            }

            Player? observer = ev.Player.CurrentlySpectating;

            if (observer == null || (observer != ev.Target && (!VisiblePlayers.TryGetValue(observer, out List<Player> visiblePlayers) || !visiblePlayers.Contains(ev.Target))))
                ev.IsVisible = false;

            return;
        }

        if (ev.IsVisible && ev.Player.RoleBase is IFpcRole observerRole && ev.Target.RoleBase is IFpcRole)
            ev.IsVisible = IsPlayerVisible(ev.Player, ev.Target, observerRole);

        SaveVisibility(ev.Player, ev.Target, ev.IsVisible);
    }

    static bool IsPlayerVisible(Player observer, Player target, IFpcRole observerRole)
    {
        Room? observerRoom;
        Room? targetRoom;

        using (PlayerBenchmark.Start())
        {
            Vector3 observerPosition = observerRole.FpcModule.Position;
            Vector3 targetPosition = target.Position;

            float sqrDistance = (observerPosition - targetPosition).sqrMagnitude;

            if (sqrDistance <= 2f * 2f)
                return true;

            if (sqrDistance > 100f * 100f)
                return false;

            if (BypassedPlayers.TryGetValue(target, out float until) && until > Time.time)
                return true;

            if (IsExceptionalObserver(observer, target))
                return true;

            float forcedVisibility = GetVisibilityRange(target);

            if (sqrDistance < forcedVisibility * forcedVisibility)
                return true;

            observerRoom = VisibilityData.GetRoom(observerPosition);
            targetRoom = VisibilityData.GetRoom(targetPosition);

            if (observerRoom != null && !target.IsDestroyed && !VisibilityData.Get(observerRoom).IsVisible(targetRoom))
                return false;
        }

        return !Config.Instance.RaycastAntiWallhack || RaycastVisibilityChecker.IsVisible(observer, target, observerRoom, targetRoom);
    }

    static void SaveVisibility(Player observer, Player target, bool visible)
    {
        if (!Config.Instance.SpectatorAntiWallhack)
            return;

        if (!VisiblePlayers.TryGetValue(observer, out List<Player> visiblePlayers))
        {
            if (!visible)
                return;

            VisiblePlayers[observer] = visiblePlayers = [];
        }

        if (!visible)
            visiblePlayers.Remove(target);
        else if (!visiblePlayers.Contains(target))
            visiblePlayers.Add(target);
    }

    static void RemoveVisibility(Player player)
    {
        BypassedPlayers.Remove(player);
        VisibilityRanges.Remove(player);
        VisiblePlayers.Remove(player);

        foreach (List<Player> visiblePlayers in VisiblePlayers.Values)
            visiblePlayers.Remove(player);
    }

    static float GetVisibilityRange(Player target)
    {
        int frame = Time.frameCount;

        if (rangeFrame != frame)
        {
            VisibilityRanges.Clear();
            rangeFrame = frame;
        }

        if (VisibilityRanges.TryGetValue(target, out float range))
            return range;

        Item? item = target.CurrentItem;
        // Night vision scopes do not count as emitting light.
        bool emitsLight = item is FirearmItem firearm ? firearm.FlashlightEnabled : item?.Base is ILightEmittingItem { IsEmittingLight: true };
        range = emitsLight ? float.PositiveInfinity : ForcedVisibilityHelper.GetForcedVisibility(target);
        VisibilityRanges[target] = range;

        return range;
    }

    static bool IsExceptionalObserver(Player observer, Player target)
    {
        if (observer.RoleBase is Scp049Role scp049 && scp049.SubroutineModule.TryGetSubroutine(out Scp049SenseAbility sense)
            && sense.HasTarget && sense.Target == target.ReferenceHub)
            return true;

        if (observer.RoleBase is Scp096Role scp096 && scp096.StateController.RageState == Scp096RageState.Enraged 
            && scp096.SubroutineModule.TryGetSubroutine(out Scp096TargetsTracker targetSub) && targetSub.Targets.Contains(target.ReferenceHub))
            return true;

        return false;
    }
}

// LabApi has no landing event. Record the same grounded transition used by the game.
[HarmonyPatch(typeof(FirstPersonMovementModule), nameof(FirstPersonMovementModule.IsGrounded), MethodType.Setter)]
internal static class PlayerLandingPatch
{
    static void Prefix(FirstPersonMovementModule __instance, bool value)
    {
        if (value && !__instance._syncGrounded && Player.Get(__instance.Hub) is Player player)
            PlayerAntiWallHackModule.BypassedPlayers[player] = Time.time + 0.5f;
    }
}
