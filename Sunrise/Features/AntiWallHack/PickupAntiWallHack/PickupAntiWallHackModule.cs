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
    const float HiddenCheckInterval = 0.1f;
    const float VisibleCheckInterval = 1f;
    const int UpdateGroups = 6;
    const int MaxRaysPerPlayer = 96;

    sealed class PlayerVisibility
    {
        // Only shown pickups are stored. A neighbour cannot reveal another neighbour.
        public readonly Dictionary<ItemPickupBase, (bool Direct, float NextCheck)> Visible = [];
        public int NextPickup;
    }

    static readonly Dictionary<Player, PlayerVisibility> Players = [];
    static readonly Dictionary<ItemPickupBase, PickupPoints> Points = [];
    static readonly List<(Pickup Pickup, Vector3 Position, bool Spawned, bool Excluded)> Pickups = [];
    static readonly List<Vector3> DirectPositions = [];
    static readonly List<(Pickup Pickup, Vector3 Position, float NextCheck)> Candidates = [];
    static int lastSlot = -1;
    static int group;

    static bool KeepSeen;

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

        KeepSeen = Config.Instance.PickupAntiWallhackKeepSeenPickupsVisible;

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
        Players.Clear();
        Pickups.Clear();
        Candidates.Clear();
        DirectPositions.Clear();
        lastSlot = -1;
        group = 0;
    }

    static void OnPickupDestroyed(ItemPickupBase pickup)
    {
        Points.Remove(pickup);

        foreach (PlayerVisibility state in Players.Values)
            state.Visible.Remove(pickup);
    }

    static void OnLeft(PlayerLeftEventArgs ev) => Players.Remove(ev.Player);

    static void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        if (!KeepSeen)
            Players.Remove(ev.Player);
    }

    static void OnChangedSpectator(PlayerChangedSpectatorEventArgs ev)
    {
        if (Config.Instance.SpectatorAntiWallhack && ev.NewTarget != ev.OldTarget && ev.Player.IsReady)
            SyncObserver(ev.Player, ev.NewTarget);
    }

    static void OnUpdate()
    {
        int slot = Mathf.FloorToInt(Time.time * (UpdateGroups / HiddenCheckInterval));

        if (slot == lastSlot)
            return;

        lastSlot = slot;
        group = (group + 1) % UpdateGroups;

        bool havePositions = false;

        foreach (Player player in Player.List)
        {
            if (player.PlayerId % UpdateGroups != group || !player.IsPlayer || !player.IsReady)
                continue;

            if (player.Role == RoleTypeId.Spectator || (player.RoleBase is not IFpcRole && player.RoleBase is not Scp079Role))
            {
                SyncObserver(player, player.Role == RoleTypeId.Spectator ? player.CurrentlySpectating : null);
                continue;
            }

            if (!havePositions)
            {
                Pickups.Clear();
                foreach (Pickup pickup in Pickup.List)
                {
                    bool spawned = pickup.IsSpawned;
                    Pickups.Add((pickup, spawned ? pickup.Base.Position : default, spawned, spawned && IsExcluded(pickup.Base)));
                }

                havePositions = true;
            }

            UpdatePlayer(player);
        }
    }

    static PlayerVisibility GetState(Player player)
    {
        if (!Players.TryGetValue(player, out var state))
            Players[player] = state = new();

        return state;
    }

    static Vector3 GetCamera(Player player) => player.RoleBase is Scp079Role scp079 ? scp079.CameraPosition : player.Camera.position;

    static void UpdatePlayer(Player player)
    {
        PlayerVisibility state = GetState(player);

        NetworkConnectionToClient connection = player.ConnectionToClient;

        Vector3 camera = GetCamera(player);

        Room? observerRoom = VisibilityData.GetRoom(camera);

        VisibilityData? visibleRooms = null;

        float range = VisibilityData.GetRangeSquared(camera);
        float now = Time.time;

        int raysLeft = MaxRaysPerPlayer;
        int start = state.NextPickup;

        bool keepSeen = KeepSeen;

        DirectPositions.Clear();
        Candidates.Clear();

        for (int offset = 0; offset < Pickups.Count; offset++)
        {
            int index = (start + offset) % Pickups.Count;
            var (pickup, position, spawned, excluded) = Pickups[index];

            ItemPickupBase item = pickup.Base;
            NetworkIdentity identity = pickup.NetworkIdentity;

            if (!spawned || excluded)
            {
                if (!keepSeen)
                    state.Visible.Remove(item);

                if (spawned)
                    SetObserver(identity, connection, CanObserve(identity, connection));

                continue;
            }

            bool known = state.Visible.TryGetValue(item, out var sight);
            float distance = (camera - position).sqrMagnitude;

            if (keepSeen && known)
            {
                bool allowed = CanObserve(identity, connection);
                SetObserver(identity, connection, allowed);
                if (allowed && distance <= NearDistanceSquared)
                    DirectPositions.Add(position);

                continue;
            }

            if (distance > range || !CanObserve(identity, connection))
            {
                state.Visible.Remove(item);
                SetObserver(identity, connection, false);
                continue;
            }

            if (distance <= NearDistanceSquared)
            {
                sight = (true, now + VisibleCheckInterval);
            }
            else if (now >= sight.NextCheck && raysLeft >= PickupPoints.PointCount)
            {
                sight = (HasLineOfSight(item, position), now + VisibleCheckInterval);
                // When the ray budget runs out, resume at the next pickup on the next turn.
                state.NextPickup = index + 1;
            }

            if (sight.Direct)
            {
                state.Visible[item] = sight;
                if (sight.NextCheck > now)
                    DirectPositions.Add(position);

                SetObserver(identity, connection, true);
            }
            else
            {
                Candidates.Add((pickup, position, known ? sight.NextCheck : now + VisibleCheckInterval));
            }
        }

        ShowNeighbours(state, connection);

        bool HasLineOfSight(ItemPickupBase item, Vector3 position)
        {
            Room? pickupRoom = VisibilityData.GetRoom(position);

            if (observerRoom != null)
            {
                visibleRooms ??= VisibilityData.Get(observerRoom);

                if (!visibleRooms.IsVisible(pickupRoom))
                    return false;
            }

            if (!Points.TryGetValue(item, out var points))
                Points[item] = points = new(item);

            return points.IsVisible(camera, VisibilityData.GetVisionMask(observerRoom, pickupRoom), ref raysLeft);
        }
    }

    static void ShowNeighbours(PlayerVisibility state, NetworkConnectionToClient connection)
    {
        foreach (var (pickup, position, nextCheck) in Candidates)
        {
            bool show = HasVisibleNeighbour(position);

            if (show)
                state.Visible[pickup.Base] = (false, nextCheck);
            else
                state.Visible.Remove(pickup.Base);

            SetObserver(pickup.NetworkIdentity, connection, show);
        }
    }

    static bool HasVisibleNeighbour(Vector3 position)
    {
        foreach (Vector3 source in DirectPositions)
        {
            if ((source - position).sqrMagnitude <= NearDistanceSquared)
                return true;
        }

        return false;
    }

    static bool CanObserve(NetworkIdentity identity, NetworkConnectionToClient connection)
        => !identity.serverOnly && identity.gameObject.activeSelf && identity.visible != Visibility.ForceHidden
            && (identity.visible == Visibility.ForceShown || NetworkServer.aoi == null || NetworkServer.aoi.OnCheckObserver(identity, connection));

    static void SetObserver(NetworkIdentity identity, NetworkConnectionToClient connection, bool show)
    {
        if (show == identity.observers.ContainsKey(connection.connectionId))
            return;

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

    static void SyncObserver(Player player, Player? target)
    {
        Players.TryGetValue(player, out PlayerVisibility own);

        PlayerVisibility? followed = null;

        if (target != null)
            Players.TryGetValue(target, out followed);

        bool showAll = player.Role == RoleTypeId.Overwatch || (player.Role == RoleTypeId.Spectator && !Config.Instance.SpectatorAntiWallhack);

        NetworkConnectionToClient connection = player.ConnectionToClient;

        foreach (Pickup pickup in Pickup.List)
        {
            if (!pickup.IsSpawned)
                continue;

            bool show = showAll || IsExcluded(pickup.Base) || followed?.Visible.ContainsKey(pickup.Base) == true
                || (KeepSeen && own?.Visible.ContainsKey(pickup.Base) == true);

            SetObserver(pickup.NetworkIdentity, connection, show && CanObserve(pickup.NetworkIdentity, connection));
        }
    }

    internal static bool AllowObserver(NetworkIdentity identity, NetworkConnectionToClient connection)
    {
        if (connection is LocalConnectionToClient || !identity.TryGetComponent(out ItemPickupBase pickup) || IsExcluded(pickup))
            return true;

        if (connection.identity == null || !ReferenceHub.TryGetHubNetID(connection.identity.netId, out ReferenceHub hub))
            return false;

        Player player = Player.Get(hub);

        bool known = Players.TryGetValue(player, out var state) && state.Visible.ContainsKey(pickup);

        if (KeepSeen && known)
            return true;

        if (player.Role == RoleTypeId.Spectator && Config.Instance.SpectatorAntiWallhack)
        {
            if (player.CurrentlySpectating is not Player target || !Players.TryGetValue(target, out var followed) || !followed.Visible.ContainsKey(pickup))
                return false;
        }
        else if (player.Role != RoleTypeId.Spectator && player.Role != RoleTypeId.Overwatch)
        {
            if (player.RoleBase is not IFpcRole && player.RoleBase is not Scp079Role)
                return false;

            Vector3 camera = GetCamera(player);
            float distance = (camera - pickup.Position).sqrMagnitude;

            if (distance > NearDistanceSquared && !(known && VisibilityData.InRange(camera, distance)))
                return false;
        }

        if (KeepSeen)
            GetState(player).Visible[pickup] = default;

        return true;
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

            case ItemType.SCP018:
                return pickup.PhysicsModule is Scp018Physics;

            case ItemType.SCP2176:
                return false; //Item has ThrownProjectile module

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
