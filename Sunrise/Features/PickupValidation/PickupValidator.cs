using InventorySystem.Items.Pickups;
using LabApi.Events.Arguments.Interfaces;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using LiteNetLib;
using Mirror;
using Mirror.LiteNetLib4Mirror;
using Sunrise.API.Backtracking;

namespace Sunrise.Features.PickupValidation;

internal static class PickupValidator
{
    internal static readonly Dictionary<Player, float> TemporaryPlayerBypass = [];
    static readonly Dictionary<Player, (Pickup Pickup, double FinishTime)> Searches = [];

    internal static void OnSearchingPickup(PlayerSearchingPickupEventArgs ev)
    {
        if (!ev.IsAllowed)
            return;

        if (TemporaryPlayerBypass.TryGetValue(ev.Player, out float time) && time > Time.time)
            return;

        Searches.Remove(ev.Player);

        if (ev.Pickup.Transform.localScale == Vector3.one && ev.Pickup is not BodyArmorPickup && !CanPickUp(ev.Player, ev.Pickup))
        {
            ev.IsAllowed = false;
            return;
        }

        float duration = ev.Pickup.Base.SearchTimeForPlayer(ev.Player.ReferenceHub);

        if (duration <= 0f)
        {
            ev.IsAllowed = false;
            return;
        }

        double latency = LiteNetLib4MirrorServer.Peers.TryGetValue(ev.Player.ConnectionToClient.connectionId, out NetPeer peer)
            ? peer.Ping * 0.001 * ev.Player.ReferenceHub.searchCoordinator.serverDelayThreshold
            : 0d;

        Searches[ev.Player] = (ev.Pickup, NetworkTime.time + duration - latency);
    }

    internal static void OnPickingUp<T>(T ev) where T : IPlayerEvent, IPickupEvent, ICancellableEvent
    {
        #pragma warning disable CS8604

        if (!Searches.TryGetValue(ev.Player, out var search) || search.Pickup != ev.Pickup)
            return;

        #pragma warning restore CS8604

        if (NetworkTime.time + 0.001 < search.FinishTime)
            ev.IsAllowed = false;

        Searches.Remove(ev.Player);
    }

    internal static void Clear()
    {
        Searches.Clear();
        TemporaryPlayerBypass.Clear();
    }

    static bool CanPickUp(Player player, Pickup pickup)
    {
        Ray ray = new(player.Camera.position, BacktrackHistory.Get(player).LatestForward);

        if (!Physics.Raycast(ray, out RaycastHit hit, 3, (int)(Mask.HitregObstacles | Mask.Pickups)))
            return false;

        if (hit.collider.GetComponentInParent<ItemPickupBase>() != pickup.Base)
            return false;

        Debug.DrawLine(ray.origin, hit.point, Colors.Blue * 50, 15);
        return true;
    }
}
