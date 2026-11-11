using HarmonyLib;
using InventorySystem.Items;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp049;
using PlayerRoles.PlayableScps.Scp096;
using PlayerRoles.PlayableScps.Scp939;
using Sunrise.API.Visibility;
using Sunrise.Features.AntiWallhack.ForcedVisibility;
using System;

namespace Sunrise.Features.AntiWallhack;

internal class AntiWallhackModule : PluginModule
{
    // After player lands their visibility should stay on for some time
    internal static readonly Dictionary<Player, float> LandingTimes = [];

    static readonly Dictionary<Player, List<Player>> VisiblePlayers = [];

    static readonly AutoBenchmark Benchmark = new("Anti Wallhack (without raycasts)");

    protected override void OnEnabled()
    {
        Handlers.PlayerEvents.ValidatedVisibility += OnValidatedVisibility;
        Handlers.PlayerEvents.Left += OnPlayerLeft;
        Handlers.PlayerEvents.ChangedRole += OnChangedRole;
    }

    protected override void OnDisabled()
    {
        Handlers.PlayerEvents.ValidatedVisibility -= OnValidatedVisibility;
        Handlers.PlayerEvents.Left -= OnPlayerLeft;
        Handlers.PlayerEvents.ChangedRole -= OnChangedRole;
    }

    protected override void OnReset()
    {
        LandingTimes.Clear();
        VisiblePlayers.Clear();
        RaycastVisibilityCache.Clear();
    }

    static void OnPlayerLeft(PlayerLeftEventArgs ev)
    {
        LandingTimes.Remove(ev.Player);
        RemoveVisibility(ev.Player);
        RaycastVisibilityCache.Clear();
    }

    static void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        LandingTimes.Remove(ev.Player);
        RemoveVisibility(ev.Player);
        RaycastVisibilityCache.Clear();
    }

    static void OnValidatedVisibility(PlayerValidatedVisibilityEventArgs ev)
    {
        if (!Config.Instance.AntiWallhack || ev.Player == ev.Target)
            return;

        if (ev.Player.Role == PlayerRoles.RoleTypeId.Spectator)
        {
            Player? observer = ev.Player.CurrentlySpectating;

            if (observer == null || (observer != ev.Target && (!VisiblePlayers.TryGetValue(observer, out List<Player> visiblePlayers) || !visiblePlayers.Contains(ev.Target))))
                ev.IsVisible = false;

            return;
        }

        if (!ev.IsVisible)
        {
            SaveVisibility(ev.Player, ev.Target, false);
            return;
        }

        if (ev.Player.RoleBase is not IFpcRole observerRole || ev.Target.RoleBase is not IFpcRole)
        {
            SaveVisibility(ev.Player, ev.Target, true);
            return;
        }

        try
        {
            Benchmark.Start();

            if (IsExceptionalCase(ev.Player, observerRole, ev.Target))
                return;

            float sqrDistance = (ev.Player.Position - ev.Target.Position).sqrMagnitude;
            float forcedVisibility = ForcedVisibilityHelper.GetForcedVisibility(ev.Target);

            if (sqrDistance < forcedVisibility * forcedVisibility)
                return;

            if (VisibilityData.Get(observerRole.FpcModule.Position) is VisibilityData visibility && !visibility.IsVisible(ev.Target))
            {
                ev.IsVisible = false;
                return;
            }

            Benchmark.Stop();

            if (Config.Instance.RaycastAntiWallhack && !RaycastVisibilityChecker.IsVisible(ev.Player, ev.Target))
                ev.IsVisible = false;
        }
        catch (Exception e)
        {
            Log.Error($"Error in {nameof(AntiWallhackModule)}.{nameof(OnValidatedVisibility)}: {e}");
        }
        finally
        {
            Benchmark.Stop();
            SaveVisibility(ev.Player, ev.Target, ev.IsVisible);
        }
    }

    static void SaveVisibility(Player observer, Player target, bool visible)
    {
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
        VisiblePlayers.Remove(player);

        foreach (List<Player> visiblePlayers in VisiblePlayers.Values)
            visiblePlayers.Remove(player);
    }

    static bool IsExceptionalCase(Player observer, IFpcRole observerRole, Player target)
    {
        //if (observerRole.FpcModule.Noclip.IsActive)
        //    return true;

        // Night vision scopes do not count as emitting light.
        if (target.CurrentItem is FirearmItem firearm)
        {
            if (firearm.FlashlightEnabled)
                return true;
        }
        else if (target.CurrentItem?.Base is ILightEmittingItem { IsEmittingLight: true })
            return true;

        if (observer.RoleBase is Scp049Role scp049 && scp049.SubroutineModule.TryGetSubroutine(out Scp049SenseAbility sense)
            && sense.HasTarget && sense.Target == target.ReferenceHub)
            return true;

        // These roles have their own visibility systems.
        return observer.RoleBase is Scp939Role
            || observer.RoleBase is Scp096Role { StateController.RageState: Scp096RageState.Enraged };
    }
}

// LabApi has no landing event. Record the same grounded transition used by the game.
[HarmonyPatch(typeof(FirstPersonMovementModule), nameof(FirstPersonMovementModule.IsGrounded), MethodType.Setter)]
internal static class PlayerLandingPatch
{
    static void Prefix(FirstPersonMovementModule __instance, bool value)
    {
        if (value && !__instance._syncGrounded && Player.Get(__instance.Hub) is Player player)
            AntiWallhackModule.LandingTimes[player] = Time.time;
    }
}
