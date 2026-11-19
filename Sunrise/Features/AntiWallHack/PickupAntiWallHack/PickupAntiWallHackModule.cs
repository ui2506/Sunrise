using HarmonyLib;
using InventorySystem.Items.Pickups;
using InventorySystem.Items.ThrowableProjectiles;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps;
using PlayerRoles.PlayableScps.Scp079;
using PlayerRoles.Spectating;
using Sunrise.API.Visibility;

namespace Sunrise.Features.AntiWallHack.PickupAntiWallHack;

internal class PickupAntiWallHackModule : PluginModule
{
    static readonly Dictionary<ItemPickupBase, VisibilityState> Pickups = [];
    static float nextUpdate;

    protected override void OnEnabled()
    {
        if (!Config.Instance.PickupAntiWallhack)
        {
            Debug.Log("PickupAntiWallhack module disabled according to the config!");
            return;
        }

        SunrisePlugin.Harmony.CreateClassProcessor(typeof(PickupObserverPatch)).Patch();

        StaticUnityMethods.OnUpdate += OnUpdate;
        ItemPickupBase.OnPickupDestroyed += OnPickupDestroyed;
        Handlers.PlayerEvents.Left += OnLeft;
        Handlers.PlayerEvents.ChangedRole += OnChangedRole;
        Handlers.PlayerEvents.ChangedSpectator += OnChangedSpectator;
    }

    protected override void OnDisabled()
    {
        StaticUnityMethods.OnUpdate -= OnUpdate;
        ItemPickupBase.OnPickupDestroyed -= OnPickupDestroyed;
        Handlers.PlayerEvents.Left -= OnLeft;
        Handlers.PlayerEvents.ChangedRole -= OnChangedRole;
        Handlers.PlayerEvents.ChangedSpectator -= OnChangedSpectator;
    }

    protected override void OnReset()
    {
        Pickups.Clear();
        nextUpdate = 0f;
    }

    static void OnPickupDestroyed(ItemPickupBase pickup) => Pickups.Remove(pickup);
    static void OnLeft(PlayerLeftEventArgs ev) => RemovePlayer(ev.Player);
    static void OnChangedRole(PlayerChangedRoleEventArgs ev) => RemovePlayer(ev.Player);
    static void OnChangedSpectator(PlayerChangedSpectatorEventArgs ev) => RemovePlayer(ev.Player);

    static void RemovePlayer(Player player)
    {
        foreach (VisibilityState state in Pickups.Values)
            state.Players.Remove(player);

        nextUpdate = 0f;
    }

    static void OnUpdate()
    {
        if (Time.time < nextUpdate)
            return;

        nextUpdate = Time.time + 0.1f;
        UpdateObservers();
    }

    static void UpdateObservers()
    {
        foreach (Pickup pickup in Pickup.List)
        {
            if (!pickup.IsSpawned || pickup.Base is ThrownProjectile)
                continue;

            NetworkIdentity identity = pickup.NetworkIdentity;

            foreach (Player player in Player.List)
            {
                if (!player.IsPlayer || !player.IsReady)
                    continue;

                bool visible = CanObserve(identity, player.ConnectionToClient) && IsVisible(pickup.Base, player);
                bool observing = identity.observers.ContainsKey(player.ConnectionToClient.connectionId);

                if (visible == observing)
                    continue;

                if (visible)
                {
                    identity.AddObserver(player.ConnectionToClient);
                }
                else
                {
                    identity.RemoveObserver(player.ConnectionToClient);
                    player.ConnectionToClient.RemoveFromObserving(identity, false);
                }
            }
        }
    }

    static bool CanObserve(NetworkIdentity identity, NetworkConnectionToClient connection)
        => !identity.serverOnly && identity.gameObject.activeSelf && identity.visible != Visibility.ForceHidden
        && (identity.visible == Visibility.ForceShown || NetworkServer.aoi == null || NetworkServer.aoi.OnCheckObserver(identity, connection));

    internal static bool AllowObserver(NetworkIdentity identity, NetworkConnectionToClient connection)
    {
        if (connection is LocalConnectionToClient || !identity.TryGetComponent(out ItemPickupBase pickup) || pickup is ThrownProjectile)
            return true;

        if (connection.identity == null || !ReferenceHub.TryGetHubNetID(connection.identity.netId, out ReferenceHub hub))
            return false;

        return IsVisible(pickup, Player.Get(hub));
    }

    static bool IsVisible(ItemPickupBase pickup, Player player)
    {
        if (player.Role == RoleTypeId.Overwatch)
            return true;

        if (player.Role == RoleTypeId.Spectator)
        {
            if (!Config.Instance.SpectatorAntiWallhack)
                return true;

            if (AntiWallHackModule.TryGetDeathPosition(player, out Vector3 deathPosition))
                return (deathPosition - pickup.Position).sqrMagnitude <= 30f * 30f;

            Player? target = player.CurrentlySpectating;

            if (target == null)
                return player.RoleBase is SpectatorRole spectator && (spectator.DeathPosition.Position - pickup.Position).sqrMagnitude <= 30f * 30f;

            if (target.RoleBase is SpectatorRole deadTarget)
                return (deadTarget.DeathPosition.Position - pickup.Position).sqrMagnitude <= 30f * 30f;

            player = target;
        }

        if (player.RoleBase is not IFpcRole && player.RoleBase is not Scp079Role)
            return false;

        if (!Pickups.TryGetValue(pickup, out VisibilityState state))
            Pickups[pickup] = state = new(pickup);

        if (state.Players.TryGetValue(player, out var cache) && cache.Expiration > Time.time)
            return cache.Visible;

        Vector3 camera = player.RoleBase is Scp079Role scp079 ? scp079.CameraPosition : player.Camera.position;

        bool visible = CheckVisibility(camera, pickup, state.Collider);

        state.Players[player] = (Time.time + (visible ? 0.5f : 0.1f), visible);

        return visible;
    }

    static bool CheckVisibility(Vector3 camera, ItemPickupBase pickup, Collider? collider)
    {
        if (VisibilityData.Get(camera) is VisibilityData visibility && !visibility.IsVisible(Room.GetRoomAtPosition(pickup.Position)))
            return false;

        Bounds bounds = collider != null && collider.enabled ? collider.bounds : new Bounds(pickup.Position, Vector3.one * 0.1f);
        Vector3 center = bounds.center;

        if (CheckLine(camera, center, pickup))
            return true;

        Vector3 extents = bounds.extents;

        return CheckLine(camera, center + Vector3.up * extents.y, pickup)
            || CheckLine(camera, center + Vector3.right * extents.x, pickup)
            || CheckLine(camera, center - Vector3.right * extents.x, pickup)
            || CheckLine(camera, center + Vector3.forward * extents.z, pickup)
            || CheckLine(camera, center - Vector3.forward * extents.z, pickup);
    }

    static bool CheckLine(Vector3 camera, Vector3 point, ItemPickupBase pickup)
        => !Physics.Linecast(camera, point, out RaycastHit hit, VisionInformation.VisionLayerMask, QueryTriggerInteraction.Ignore)
        || hit.transform.IsChildOf(pickup.transform);

    class VisibilityState(ItemPickupBase pickup)
    {
        public readonly Collider? Collider = pickup.GetComponentInChildren<Collider>();
        public readonly Dictionary<Player, (float Expiration, bool Visible)> Players = [];
    }
}

[HarmonyPatch(typeof(NetworkIdentity), nameof(NetworkIdentity.AddObserver))]
internal static class PickupObserverPatch
{
    static bool Prefix(NetworkIdentity __instance, NetworkConnectionToClient __0)
        => PickupAntiWallHackModule.AllowObserver(__instance, __0);
}
