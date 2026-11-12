using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.NetworkMessages;
using Sunrise.API.Detections;

namespace Sunrise.Features.AntiBigStep;

internal class AntiBigStepModule : PluginModule
{
    static readonly Dictionary<ReferenceHub, MovementData> Players = [];

    protected override void OnEnabled()
    {
        if (!Config.Instance.AntiBigStep)
        {
            Debug.Log("AntiBigStep module disabled according to the config!");
            return;
        }

        SunrisePlugin.Harmony.CreateClassProcessor(typeof(FpcFromClientMessagePatch)).Patch();
        SunrisePlugin.Harmony.CreateClassProcessor(typeof(ServerOverridePositionPatch)).Patch();

        Handlers.PlayerEvents.ChangedRole += OnChangedRole;
        Handlers.PlayerEvents.Left += OnLeft;
    }

    protected override void OnDisabled()
    {
        if (!Config.Instance.AntiBigStep)
            return;

        Handlers.PlayerEvents.ChangedRole -= OnChangedRole;
        Handlers.PlayerEvents.Left -= OnLeft;
    }

    protected override void OnReset() => Players.Clear();

    static void OnChangedRole(PlayerChangedRoleEventArgs ev) => Players.Remove(ev.Player.ReferenceHub);
    static void OnLeft(PlayerLeftEventArgs ev) => Players.Remove(ev.Player.ReferenceHub);

    internal static void OnPositionOverridden(ReferenceHub hub)
    {
        if (!Players.TryGetValue(hub, out MovementData data))
            Players[hub] = data = new();

        data.HasPosition = false;
        data.IgnoreUntil = Time.time + 0.5f;
    }

    internal static void OnMovement(ReferenceHub hub, FpcSyncData sync)
    {
        if (!sync._bitPosition)
            return;

        if (hub.roleManager.CurrentRole is not IFpcRole role || !role.FpcModule.ModuleReady)
            return;

        FirstPersonMovementModule module = role.FpcModule;

        if (hub.roleManager.CurrentRole.ActiveTime < 3f || module.Noclip.IsActive || module.Noclip.RecentlyActive)
        {
            Players.Remove(hub);
            return;
        }

        if (!Players.TryGetValue(hub, out MovementData data))
            Players[hub] = data = new();

        Vector3 position = sync._position.Position;

        float now = Time.time;
        float rise = position.y - data.Position.y;
        float elapsed = now - data.Time;

        data.Position = position;
        data.Time = now;
        data.Waypoint = sync._position.WaypointId;
        data.HasPosition = true;

        if (!(data.HasPosition && data.Waypoint == sync._position.WaypointId) || now < data.IgnoreUntil || now < data.NextWarning)
            return;

        float upwardSpeed = Mathf.Max(module.JumpSpeed * module.Motor.JumpController.JumpMultiplier, module.Motor.MoveDirection.y);
        float serverDifference = position.y - module.Position.y;

        if (!IsSuspicious(rise, elapsed, upwardSpeed, serverDifference))
            return;

        foreach (Elevator elevator in Elevator.List)
        {
            Bounds bounds = elevator.WorldSpaceRelativeBounds.Bounds;

            if (bounds.Contains(module.Position) || bounds.Contains(position))
                return;
        }

        data.NextWarning = now + 1f;

        Player player = Player.Get(hub);

        string details = $"Room: {player.Room?.Name ?? MapGeneration.RoomName.Unnamed} {player.Room?.Transform.InverseTransformPoint(player.Position) ?? Vector3.zero}\nrise: {rise:F2}, interval: {elapsed:F3}s, server difference: {serverDifference:F2}, step offset: {module.CharController.stepOffset:F2}.";

        if (serverDifference > 1f)
        {
            DetectionHandler.Handle(player, "BigStep", details);
        }
        else
        {
            player.Position = data.Position;

            Log.Warn($"[AntiBigStep] Possible BigStep detected: Player {player.Nickname} ({player.UserId})\n{details}");
        }
    }

    internal static bool IsSuspicious(float rise, float elapsed, float upwardSpeed, float serverDifference)
        => elapsed >= 0f && rise >= 0.75f && serverDifference >= 0.75f
            && rise > Mathf.Max(0f, upwardSpeed) * elapsed + 0.35f;

    class MovementData
    {
        public Vector3 Position;
        public float Time;
        public byte Waypoint;
        public bool HasPosition;
        public float IgnoreUntil;
        public float NextWarning;
    }
}
