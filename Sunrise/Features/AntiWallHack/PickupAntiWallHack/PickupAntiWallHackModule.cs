using HarmonyLib;
using InventorySystem.Items.Firearms;
using InventorySystem.Items.Pickups;
using InventorySystem.Items.ThrowableProjectiles;
using InventorySystem.Items.Usables.Scp244;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp079;
using PlayerRoles.Spectating;
using Sunrise.API.Visibility;

namespace Sunrise.Features.AntiWallHack.PickupAntiWallHack;

internal class PickupAntiWallHackModule : PluginModule
{
    const float UpdateInterval = 0.1f;

    static readonly Dictionary<ItemPickupBase, VisibilityState> Pickups = [];
    static readonly List<Player> PendingPlayers = [];
    static readonly List<Player> Observers = [];
    const int UpdateGroups = 6;
    static int lastSlot;

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
        Pickups.Clear();
        PendingPlayers.Clear();
        Observers.Clear();
        lastSlot = CurrentSlot;
    }

    static void OnPickupDestroyed(ItemPickupBase pickup)
    {
        Pickups.Remove(pickup);

        foreach (Player player in Observers)
            player.ConnectionToClient.RemoveFromObserving(pickup.netIdentity, false);
    }

    static void OnLeft(PlayerLeftEventArgs ev)
    {
        RemovePlayer(ev.Player);
        PendingPlayers.Remove(ev.Player);
    }

    static void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        RemovePlayer(ev.Player);
        QueueUpdate(ev.Player);
    }

    static void OnChangedSpectator(PlayerChangedSpectatorEventArgs ev)
    {
        if (Config.Instance.SpectatorAntiWallhack && ev.NewTarget != ev.OldTarget)
            QueueUpdate(ev.Player);
    }

    static void QueueUpdate(Player player)
    {
        if (!PendingPlayers.Contains(player))
            PendingPlayers.Add(player);
    }

    static void RemovePlayer(Player player)
    {
        foreach (VisibilityState state in Pickups.Values)
            state.Players.Remove(player);
    }

    static int CurrentSlot => Mathf.FloorToInt(Time.time * (UpdateGroups / UpdateInterval));

    static void OnUpdate()
    {
        int slot = CurrentSlot;
        int elapsed = slot - lastSlot;

        if (elapsed == 0 && PendingPlayers.Count == 0)
            return;

        lastSlot = slot;
        Observers.Clear();

        foreach (Player player in Player.List)
        {
            // Each connection is due once per cycle. A slow frame also handles the skipped groups.
            int slotsSinceTurn = (slot % UpdateGroups + UpdateGroups - player.PlayerId % UpdateGroups) % UpdateGroups;

            if (slotsSinceTurn >= elapsed && !PendingPlayers.Contains(player))
                continue;

            if (player.IsPlayer && player.IsReady)
                Observers.Add(player);
        }

        PendingPlayers.Clear();

        if (Observers.Count == 0)
            return;

        foreach (Pickup pickup in Pickup.List)
        {
            if (!pickup.IsSpawned || IsExcluded(pickup.Base))
                continue;

            NetworkIdentity identity = pickup.NetworkIdentity;
            bool available = !identity.serverOnly && identity.gameObject.activeSelf && identity.visible != Visibility.ForceHidden;

            if (!available && identity.observers.Count == 0)
                continue;

            InterestManagementBase? aoi = identity.visible == Visibility.ForceShown ? null : NetworkServer.aoi;

            foreach (Player player in Observers)
            {
                NetworkConnectionToClient connection = player.ConnectionToClient;

                bool visible = available && (aoi == null || aoi.OnCheckObserver(identity, connection)) && IsVisible(pickup.Base, player, slot);

                if (identity.observers.ContainsKey(connection.connectionId) == visible)
                    continue;

                if (visible)
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

    internal static bool AllowObserver(NetworkIdentity identity, NetworkConnectionToClient connection)
    {
        if (connection is LocalConnectionToClient || !identity.TryGetComponent(out ItemPickupBase pickup) || IsExcluded(pickup))
            return true;

        if (connection.identity == null || !ReferenceHub.TryGetHubNetID(connection.identity.netId, out ReferenceHub hub))
            return false;

        return IsVisible(pickup, Player.Get(hub), CurrentSlot);
    }

    static bool IsVisible(ItemPickupBase pickup, Player player, int slot)
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

        Vector3 camera = player.RoleBase is Scp079Role scp079 ? scp079.CameraPosition : player.Camera.position;

        float sqrDistance = (camera - pickup.Position).sqrMagnitude;

        if (sqrDistance <= 3f * 3f)
            return true;

        if (!VisibilityData.InRange(camera, sqrDistance))
            return false;

        if (!Pickups.TryGetValue(pickup, out VisibilityState state))
            Pickups[pickup] = state = new(pickup);

        if (state.Players.TryGetValue(player, out var cache) && cache.Expiration > slot)
            return cache.Visible;

        bool visible = CheckVisibility(camera, pickup, state);

        state.Players[player] = (slot + (visible ? 5 : 1) * UpdateGroups, visible);

        return visible;
    }

    static bool CheckVisibility(Vector3 camera, ItemPickupBase pickup, VisibilityState state)
    {
        Room? observerRoom = VisibilityData.GetRoom(camera);
        Room? targetRoom = state.GetRoom();

        if (observerRoom != null && !VisibilityData.Get(observerRoom).IsVisible(targetRoom))
            return false;

        int mask = VisibilityData.GetVisionMask(observerRoom, targetRoom);

        Bounds bounds = state.GetBounds();
        Vector3 center = bounds.center;

        if (CheckLine(camera, center, pickup, mask))
            return true;

        Vector3 extents = bounds.extents;

        return CheckLine(camera, center + Vector3.up * extents.y, pickup, mask)
            || CheckLine(camera, center + Vector3.right * extents.x, pickup, mask)
            || CheckLine(camera, center - Vector3.right * extents.x, pickup, mask)
            || CheckLine(camera, center + Vector3.forward * extents.z, pickup, mask)
            || CheckLine(camera, center - Vector3.forward * extents.z, pickup, mask);
    }

    static bool CheckLine(Vector3 camera, Vector3 point, ItemPickupBase pickup, int mask)
        => !Physics.Linecast(camera, point, out RaycastHit hit, mask, QueryTriggerInteraction.Ignore)
        || hit.transform.IsChildOf(pickup.transform);

    class VisibilityState(ItemPickupBase pickup)
    {
        public readonly Dictionary<Player, (int Expiration, bool Visible)> Players = [];
        Vector3? _position;
        Quaternion _rotation;
        Vector3 _scale;
        FirearmWorldmodel? _model;
        Renderer[]? _renderers;
        Collider[]? _colliders;
        uint _attachments;
        float _nextDraw;
        int _roomFrame = -1;
        Room? _room;
        Bounds _bounds;

        public Room? GetRoom()
        {
            int frame = Time.frameCount;

            if (_roomFrame != frame)
            {
                _room = VisibilityData.GetRoom(pickup.Position);
                _roomFrame = frame;
            }

            return _room;
        }

        public Bounds GetBounds()
        {
            Vector3 position = pickup.Position;
            Quaternion rotation = pickup.Rotation;
            Vector3 scale = pickup.transform.lossyScale;

            FirearmWorldmodel? model = (pickup as InventorySystem.Items.Firearms.FirearmPickup)?._worldmodelInstance;

            uint attachments = model != null ? model.AttachmentCode : 0;

            if (_position != position || _rotation != rotation || _scale != scale || _model != model || _attachments != attachments)
            {
                _position = position;
                _rotation = rotation;
                _scale = scale;
                _model = model;
                _attachments = attachments;
                _bounds = CalculateBounds(model, position);
                _bounds.extents += Vector3.one * 0.15f;
            }

            if (Config.Instance.DebugPrimitives && Time.time >= _nextDraw)
            {
                _nextDraw = Time.time + UpdateInterval;

                Vector3 center = _bounds.center;
                Vector3 extents = _bounds.extents;

                Debug.DrawPoint(center, Colors.Orange * 50, 0.11f);
                Debug.DrawPoint(center + Vector3.up * extents.y, Colors.Orange * 50, 0.11f);
                Debug.DrawPoint(center + Vector3.right * extents.x, Colors.Orange * 50, 0.11f);
                Debug.DrawPoint(center - Vector3.right * extents.x, Colors.Orange * 50, 0.11f);
                Debug.DrawPoint(center + Vector3.forward * extents.z, Colors.Orange * 50, 0.11f);
                Debug.DrawPoint(center - Vector3.forward * extents.z, Colors.Orange * 50, 0.11f);
            }

            return _bounds;
        }

        Bounds CalculateBounds(FirearmWorldmodel? model, Vector3 position)
        {
            Bounds bounds = default;

            foreach (Renderer renderer in model != null ? model.Renderers : (_renderers ??= pickup.GetComponentsInChildren<Renderer>(true)))
            {
                if (renderer != null && renderer is MeshRenderer or SkinnedMeshRenderer && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    Include(renderer.bounds);
            }

            if (bounds.size != Vector3.zero)
                return bounds;

            foreach (Collider collider in model != null && model.Colliders.Length > 0 ? model.Colliders : (_colliders ??= pickup.GetComponentsInChildren<Collider>(true)))
            {
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                    Include(collider.bounds);
            }

            return bounds.size != Vector3.zero ? bounds : new Bounds(position, Vector3.one * 0.1f);

            void Include(Bounds part)
            {
                if (part.size == Vector3.zero)
                    return;

                if (bounds.size == Vector3.zero)
                    bounds = part;
                else
                    bounds.Encapsulate(part);
            }
        }
    }
}

[HarmonyPatch(typeof(NetworkIdentity), nameof(NetworkIdentity.AddObserver))]
internal static class PickupObserverPatch
{
    static bool Prefix(NetworkIdentity __instance, NetworkConnectionToClient __0) => PickupAntiWallHackModule.AllowObserver(__instance, __0);
}
