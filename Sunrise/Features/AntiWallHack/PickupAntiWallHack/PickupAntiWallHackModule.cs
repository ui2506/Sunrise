using HarmonyLib;
using InventorySystem.Items.Pickups;
using InventorySystem.Items.ThrowableProjectiles;
using InventorySystem.Items.Usables.Scp244;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp079;
using Sunrise.API.Visibility;

namespace Sunrise.Features.AntiWallHack.PickupAntiWallHack;

internal class PickupAntiWallHackModule : PluginModule
{
    const float NearDistanceSquared = 3f * 3f;
    const int UpdateGroups = 6;

    static readonly Dictionary<ItemPickupBase, PickupPoints> Points = [];
    static readonly Dictionary<Player, List<ItemPickupBase>> VisiblePickups = [];
    static readonly Dictionary<Player, List<ItemPickupBase>> DirectlyVisible = [];
    static readonly Dictionary<Player, float> NextVisibleCheck = [];
    static readonly List<ItemPickupBase> Candidates = [];
    static int lastSlot = -1;
    static int group;

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

        base.OnEnabled();
    }

    protected override void OnDisabled()
    {
        StaticUnityMethods.OnUpdate -= OnUpdate;
        ItemPickupBase.OnPickupDestroyed -= OnPickupDestroyed;
        Handlers.PlayerEvents.Left -= OnLeft;
        Handlers.PlayerEvents.ChangedRole -= OnChangedRole;
        Handlers.PlayerEvents.ChangedSpectator -= OnChangedSpectator;

        base.OnDisabled();
    }

    protected override void OnReset()
    {
        Points.Clear();
        VisiblePickups.Clear();
        DirectlyVisible.Clear();
        NextVisibleCheck.Clear();
        Candidates.Clear();
        lastSlot = -1;
        group = 0;
    }

    static void OnPickupDestroyed(ItemPickupBase pickup)
    {
        Points.Remove(pickup);

        foreach (var list in VisiblePickups.Values)
            list.Remove(pickup);

        foreach (var list in DirectlyVisible.Values)
            list.Remove(pickup);
    }

    static void OnChangedRole(PlayerChangedRoleEventArgs ev) => RemovePlayer(ev.Player);

    static void OnLeft(PlayerLeftEventArgs ev) => RemovePlayer(ev.Player);

    static void RemovePlayer(Player player)
    {
        VisiblePickups.Remove(player);
        DirectlyVisible.Remove(player);
        NextVisibleCheck.Remove(player);
    }

    static void OnChangedSpectator(PlayerChangedSpectatorEventArgs ev)
    {
        if (Config.Instance.SpectatorAntiWallhack && ev.NewTarget != ev.OldTarget && ev.Player.IsReady)
            SyncObservers(ev.Player, ev.NewTarget);
    }

    static void OnUpdate()
    {
        int slot = Mathf.FloorToInt(Time.time * (UpdateGroups / 0.1f));

        if (slot == lastSlot)
            return;

        lastSlot = slot;
        group = (group + 1) % UpdateGroups;

        foreach (Player player in Player.List)
        {
            if (player.PlayerId % UpdateGroups != group || !player.IsPlayer || !player.IsReady)
                continue;

            if (player.Role == RoleTypeId.Spectator)
            {
                SyncObservers(player, player.CurrentlySpectating);
                continue;
            }

            if (player.RoleBase is IFpcRole || player.RoleBase is Scp079Role)
                UpdateVisiblePickups(player);

            SyncObservers(player, player);
        }
    }

    static void UpdateVisiblePickups(Player player)
    {
        if (!VisiblePickups.TryGetValue(player, out var visible))
            VisiblePickups[player] = visible = [];

        if (!DirectlyVisible.TryGetValue(player, out var direct))
            DirectlyVisible[player] = direct = [];

        bool recheckVisible = !NextVisibleCheck.TryGetValue(player, out float next) || Time.time >= next;

        if (recheckVisible)
            NextVisibleCheck[player] = Time.time + 1f;

        Vector3 camera = player.RoleBase is Scp079Role scp079 ? scp079.CameraPosition : player.Camera.position;

        float range = VisibilityData.GetRangeSquared(camera);

        Room? room = VisibilityData.GetRoom(camera);

        Candidates.Clear();

        foreach (Pickup pickup in Pickup.List)
        {
            ItemPickupBase item = pickup.Base;

            float distance = (camera - item.Position).sqrMagnitude;

            if (!pickup.IsSpawned || distance > range || IsExcluded(item) || !CanObserve(pickup.NetworkIdentity, player.ConnectionToClient))
            {
                direct.Remove(item);
                continue;
            }

            bool seen = distance <= NearDistanceSquared;

            if (!seen && !recheckVisible && visible.Contains(item))
            {
                if (!direct.Contains(item))
                    Candidates.Add(item);

                continue;
            }

            if (!seen)
            {
                if (!Points.TryGetValue(item, out var points))
                    Points[item] = points = new(item);

                seen = points.IsVisible(camera, room);
            }

            if (!seen)
            {
                direct.Remove(item);
                Candidates.Add(item);
            }
            else if (!direct.Contains(item))
            {
                direct.Add(item);
            }
        }

        visible.Clear();
        visible.AddRange(direct);

        foreach (ItemPickupBase item in Candidates)
        {
            foreach (ItemPickupBase source in direct)
            {
                if ((source.Position - item.Position).sqrMagnitude <= NearDistanceSquared)
                {
                    visible.Add(item);
                    break;
                }
            }
        }
    }

    static bool CanObserve(NetworkIdentity identity, NetworkConnectionToClient connection)
        => !identity.serverOnly && identity.gameObject.activeSelf && identity.visible != Visibility.ForceHidden
            && (identity.visible == Visibility.ForceShown || NetworkServer.aoi == null || NetworkServer.aoi.OnCheckObserver(identity, connection));

    static void SyncObservers(Player player, Player? target)
    {
        List<ItemPickupBase>? visible = null;

        if (target != null)
            VisiblePickups.TryGetValue(target, out visible);

        bool showAll = player.Role == RoleTypeId.Overwatch || (player.Role == RoleTypeId.Spectator && !Config.Instance.SpectatorAntiWallhack);

        NetworkConnectionToClient connection = player.ConnectionToClient;

        foreach (Pickup pickup in Pickup.List)
        {
            if (!pickup.IsSpawned)
                continue;

            NetworkIdentity identity = pickup.NetworkIdentity;

            bool show = (showAll || IsExcluded(pickup.Base) || visible?.Contains(pickup.Base) == true) && CanObserve(identity, connection);

            if (show == identity.observers.ContainsKey(connection.connectionId))
                continue;

            if (show)
            {
                identity.AddObserver(connection);
            }
            else
            {
                identity.RemoveObserver(connection);
                connection.RemoveFromObserving(identity, false);
            }
        }
    }

    internal static bool AllowObserver(NetworkIdentity identity, NetworkConnectionToClient connection)
    {
        if (connection is LocalConnectionToClient || !identity.TryGetComponent(out ItemPickupBase pickup) || IsExcluded(pickup))
            return true;

        if (connection.identity == null || !ReferenceHub.TryGetHubNetID(connection.identity.netId, out ReferenceHub hub))
            return false;

        Player player = Player.Get(hub);
        if (player.Role == RoleTypeId.Overwatch)
            return true;

        if (player.Role == RoleTypeId.Spectator)
        {
            if (!Config.Instance.SpectatorAntiWallhack)
                return true;

            return player.CurrentlySpectating is Player target && VisiblePickups.TryGetValue(target, out var items) && items.Contains(pickup);
        }

        if (player.RoleBase is not IFpcRole && player.RoleBase is not Scp079Role)
            return false;

        Vector3 camera = player.RoleBase is Scp079Role scp079 ? scp079.CameraPosition : player.Camera.position;

        float distance = (camera - pickup.Position).sqrMagnitude;

        return distance <= NearDistanceSquared || (VisibilityData.InRange(camera, distance)
            && VisiblePickups.TryGetValue(player, out var visible) && visible.Contains(pickup));
    }

    static bool IsExcluded(ItemPickupBase pickup)
    {
        switch (pickup.Info.ItemId)
        {
            case ItemType.Radio:
                return true;

            case ItemType.SCP244a:
            case ItemType.SCP244b:
                return pickup is Scp244DeployablePickup scp244 && scp244.State == Scp244State.Active;

            default:
                return pickup is ThrownProjectile;
        }
    }
}

[HarmonyPatch(typeof(NetworkIdentity), nameof(NetworkIdentity.AddObserver))]
internal static class PickupObserverPatch
{
    static bool Prefix(NetworkIdentity __instance, NetworkConnectionToClient __0) => PickupAntiWallHackModule.AllowObserver(__instance, __0);
}
