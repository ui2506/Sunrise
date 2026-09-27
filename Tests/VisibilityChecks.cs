global using System;
global using System.Collections.Generic;
global using Sunrise.EntryPoint;
global using Sunrise.Utility;
global using UnityEngine;
global using Handlers = LabApi.Events.Handlers;
global using Log = Sunrise.Utility.TestLog;

using System.Reflection;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp049;
using PlayerRoles.PlayableScps.Scp096;
using PlayerRoles.PlayableScps.Scp939;
using Sunrise.API.Visibility;
using Sunrise.Features.AntiWallhack;
using Sunrise.Features.AntiWallhack.ForcedVisibility;

// Run the production handler with controlled game boundaries, without a Unity server.
internal static class VisibilityChecks
{
    static int _checks;

    static void Main()
    {
        Config config = new();
        AntiWallhackModule module = new();
        Player observer = new() { RoleBase = new TestRole(), Position = new(0) };
        Player target = new() { RoleBase = new TestRole(), Position = new(50) };
        module.Enable();
        module.Enable();

        Check(Handlers.PlayerEvents.Subscribers == 1, "Enable must not duplicate subscriptions");
        Check(!Visible(observer, target), "An occluded target must be hidden");

        config.AntiWallhack = false;
        Check(Visible(observer, target), "Disabled anti-wallhack must preserve visibility");
        config.AntiWallhack = true;
        Check(!Visible(observer, target, false), "Vanilla invisibility must never be undone");
        Check(Visible(observer, observer), "A player must not hide themselves");

        observer.RoleBase = new object();
        Check(Visible(observer, target), "Spectators must retain vanilla visibility");
        observer.RoleBase = new TestRole();
        target.RoleBase = new object();
        Check(Visible(observer, target), "Non-FPC targets must retain vanilla visibility");
        target.RoleBase = new TestRole();
        ((TestRole)observer.RoleBase).FpcModule.Noclip.IsActive = true;
        Check(Visible(observer, target), "Noclip must bypass custom visibility");
        ((TestRole)observer.RoleBase).FpcModule.Noclip.IsActive = false;

        ForcedVisibilityHelper.Range = 51;
        Check(Visible(observer, target), "Audible targets must remain visible");
        ForcedVisibilityHelper.Range = 50;
        Check(!Visible(observer, target), "The original strict sound-range boundary must be preserved");
        ForcedVisibilityHelper.Range = 0;

        VisibilityData.Data = new() { Visible = true };
        Check(!Visible(observer, target), "Raycasts must still hide targets in visible rooms");
        config.RaycastAntiWallhack = false;
        Check(Visible(observer, target), "Disabling raycasts must retain room visibility");
        VisibilityData.Data.Visible = false;
        Check(!Visible(observer, target), "Disabling raycasts must not disable room checks");
        config.RaycastAntiWallhack = true;
        RaycastVisibilityChecker.Visible = true;
        Check(!Visible(observer, target), "Room rejection must precede raycasts");
        VisibilityData.Data = null;
        Check(Visible(observer, target), "Missing room data must allow raycast visibility");
        RaycastVisibilityChecker.Visible = false;
        Check(!Visible(observer, target), "Raycasts must also work without room data");

        target.CurrentItem = new FirearmItem { FlashlightEnabled = true };
        Check(Visible(observer, target), "A weapon flashlight must reveal its owner");
        target.CurrentItem = new FirearmItem { Base = new TestLight() };
        Check(!Visible(observer, target), "Night vision light must not count as a flashlight");
        target.CurrentItem = new Item { Base = new TestLight() };
        Check(Visible(observer, target), "Other emitting items must reveal their owner");
        target.CurrentItem = null;

        observer.RoleBase = new Scp939Role();
        Check(Visible(observer, target), "SCP-939 must retain its visibility system");
        Scp049Role scp049 = new();
        observer.RoleBase = scp049;
        scp049.SubroutineModule.Sense = new() { HasTarget = true, Target = target.ReferenceHub };
        Check(Visible(observer, target), "SCP-049 must see its sense target");
        scp049.SubroutineModule.Sense.Target = observer.ReferenceHub;
        Check(!Visible(observer, target), "SCP-049 sense must not reveal unrelated players");
        Scp096Role scp096 = new();
        observer.RoleBase = scp096;
        scp096.StateController.RageState = Scp096RageState.Enraged;
        Check(Visible(observer, target), "Enraged SCP-096 must retain vanilla visibility");
        scp096.StateController.RageState = Scp096RageState.Calm;
        Check(!Visible(observer, target), "Calm SCP-096 must still use normal checks");

        VisibilityData.Throw = true;
        Check(Visible(observer, target) && TestLog.Errors == 1, "A failed visibility check must preserve visibility and log the error");
        VisibilityData.Throw = false;
        Time.time = 12;
        FirstPersonMovementModule movement = ((TestRole)observer.RoleBase).FpcModule;
        movement.Hub = observer.ReferenceHub;
        MethodInfo landing = typeof(PlayerLandingPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)!;
        landing.Invoke(null, [movement, true]);
        Check(AntiWallhackModule.LandingTimes[observer] == 12, "A landing must record its time");
        movement._syncGrounded = true;
        Time.time = 13;
        landing.Invoke(null, [movement, true]);
        Check(AntiWallhackModule.LandingTimes[observer] == 12, "Repeated grounded state must not extend landing visibility");

        Handlers.PlayerEvents.ChangeRole(observer);
        Check(!AntiWallhackModule.LandingTimes.ContainsKey(observer), "Role changes must clear landing state");
        AntiWallhackModule.LandingTimes[observer] = 1;
        Handlers.ServerEvents.Wait();
        Check(AntiWallhackModule.LandingTimes.Count == 0, "Round reset must clear landing state");
        AntiWallhackModule.LandingTimes[observer] = 1;
        Handlers.PlayerEvents.Leave(observer);
        Check(AntiWallhackModule.LandingTimes.Count == 0, "Disconnect must clear landing state");
        int clears = RaycastVisibilityCache.Clears;
        module.Disable();
        module.Disable();
        Check(Handlers.PlayerEvents.Subscribers == 0, "Disable must unsubscribe visibility");
        Check(RaycastVisibilityCache.Clears == clears + 1, "Disable must reset cache exactly once");
        Check(Visible(observer, target), "A disabled module must not change visibility");
        module.Enable();
        Check(!Visible(observer, target), "The module must work after re-enabling");
        module.Disable();
        Console.WriteLine($"{_checks} visibility and lifecycle checks passed.");
    }

    static bool Visible(Player observer, Player target, bool visible = true)
    {
        PlayerValidatedVisibilityEventArgs ev = new() { Player = observer, Target = target, IsVisible = visible };
        Handlers.PlayerEvents.Validate(ev);
        return ev.IsVisible;
    }

    static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
        _checks++;
    }
}

// Only engine/API boundaries are stubbed; the handler and module lifecycle above are linked source files.
namespace UnityEngine
{
    public static class Time { public static float time; }
    public readonly record struct Vector3(float X)
    {
        public float sqrMagnitude => X * X;
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X);
    }
}
namespace HarmonyLib
{
    public enum MethodType { Setter }
    public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name, MethodType method) { } }
}
namespace Sunrise.Utility
{
    public class AutoBenchmark { public AutoBenchmark(string name) { } public void Start() { } public void Stop() { } }
    public static class TestLog { public static int Errors; public static void Error(string message) => Errors++; }
}
namespace InventorySystem.Items
{
    public interface ILightEmittingItem { bool IsEmittingLight { get; } }
    public class TestLight : ILightEmittingItem { public bool IsEmittingLight => true; }
}
internal class TestLight : InventorySystem.Items.TestLight { }
public class ReferenceHub { public Player Player = null!; }
namespace LabApi.Features.Wrappers
{
    public class Item { public object? Base; }
    public class FirearmItem : Item { public bool FlashlightEnabled; }
    public class Player
    {
        public Player() => ReferenceHub = new() { Player = this };
        public ReferenceHub ReferenceHub;
        public object RoleBase = null!;
        public Item? CurrentItem;
        public Vector3 Position;
        public static Player Get(ReferenceHub hub) => hub.Player;
    }
}
namespace PlayerRoles.FirstPersonControl
{
    public interface IFpcRole { FirstPersonMovementModule FpcModule { get; } }
    public class TestRole : IFpcRole { public FirstPersonMovementModule FpcModule { get; } = new(); }
    public class FirstPersonMovementModule
    {
        public ReferenceHub Hub = null!;
        public bool _syncGrounded;
        public bool IsGrounded { get; set; }
        public Noclip Noclip = new();
        public Vector3 Position;
    }
    public class Noclip { public bool IsActive; }
}
namespace PlayerRoles.PlayableScps.Scp049
{
    public class Scp049Role : TestRole { public Subroutines SubroutineModule = new(); }
    public class Scp049SenseAbility { public bool HasTarget; public ReferenceHub Target = null!; }
    public class Subroutines
    {
        public Scp049SenseAbility? Sense;
        public bool TryGetSubroutine(out Scp049SenseAbility sense) { sense = Sense!; return Sense != null; }
    }
}
namespace PlayerRoles.PlayableScps.Scp096
{
    public class Scp096Role : TestRole { public StateController StateController = new(); }
    public class StateController { public Scp096RageState RageState; }
    public enum Scp096RageState { Calm, Enraged }
}
namespace PlayerRoles.PlayableScps.Scp939 { public class Scp939Role : TestRole { } }
namespace Sunrise.Features.AntiWallhack.ForcedVisibility
{
    public static class ForcedVisibilityHelper { public static float Range; public static float GetForcedVisibility(Player player) => Range; }
}
namespace Sunrise.API.Visibility
{
    public class VisibilityData
    {
        public static VisibilityData? Data = new();
        public static bool Throw;
        public bool Visible;
        public static VisibilityData? Get(Vector3 position) => Throw ? throw new Exception("Test failure") : Data;
        public bool IsVisible(Player player) => Visible;
    }
}
namespace Sunrise.Features.AntiWallhack
{
    public static class RaycastVisibilityChecker { public static bool Visible; public static bool IsVisible(Player a, Player b) => Visible; }
    public static class RaycastVisibilityCache { public static int Clears; public static void Clear() => Clears++; }
}
namespace LabApi.Events.Arguments.PlayerEvents
{
    public class PlayerValidatedVisibilityEventArgs { public Player Player = null!; public Player Target = null!; public bool IsVisible; }
    public class PlayerLeftEventArgs { public Player Player = null!; }
    public class PlayerChangedRoleEventArgs { public Player Player = null!; }
}
namespace LabApi.Events.Handlers
{
    public static class PlayerEvents
    {
        public static event Action<PlayerValidatedVisibilityEventArgs>? ValidatedVisibility;
        public static event Action<PlayerLeftEventArgs>? Left;
        public static event Action<PlayerChangedRoleEventArgs>? ChangedRole;
        public static int Subscribers => ValidatedVisibility?.GetInvocationList().Length ?? 0;
        public static void Validate(PlayerValidatedVisibilityEventArgs ev) => ValidatedVisibility?.Invoke(ev);
        public static void Leave(Player player) => Left?.Invoke(new() { Player = player });
        public static void ChangeRole(Player player) => ChangedRole?.Invoke(new() { Player = player });
    }
    public static class ServerEvents
    {
        public static event Action? WaitingForPlayers;
        public static void Wait() => WaitingForPlayers?.Invoke();
    }
}
