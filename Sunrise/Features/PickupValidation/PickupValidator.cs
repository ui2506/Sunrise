using InventorySystem.Items.Pickups;
using LabApi.Events.Arguments.Interfaces;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using Sunrise.API.Backtracking;

namespace Sunrise.Features.PickupValidation;

internal static class PickupValidator
{
    internal static readonly Dictionary<Player, float> TemporaryPlayerBypass = [];
    static readonly Dictionary<Player, (Pickup Pickup, float FinishTime)> Searches = [];

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

        if (duration < 0.1f)
            return;

        Searches[ev.Player] = (ev.Pickup, Time.time + duration);
    }

    internal static void OnPickingUp<T>(T ev) where T : IPlayerEvent, IPickupEvent, ICancellableEvent
    {
        #pragma warning disable CS8604

        if (!Searches.TryGetValue(ev.Player, out var search) || search.Pickup != ev.Pickup)
            return;

        #pragma warning restore CS8604

        float pickedUpTime = search.FinishTime - Time.time;

        double latency = InteractionRaycast.GetLatency(ev.Player);

        string details = $"Pickup time: {pickedUpTime}\nPickup latency: {latency}\nPickup and latency: {(pickedUpTime - latency):F2}";

        if (pickedUpTime - latency > 0.1f)
        {
            Config.Instance.FastPickupDetectConfig.Handle(ev.Player, "FastPickup", details);
            ev.IsAllowed = false;
        }

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

        const int mask = (int)(Mask.HitregObstacles | Mask.Pickups);
        if (!Physics.Raycast(ray, out RaycastHit hit, 3f, mask) || hit.collider.GetComponentInParent<ItemPickupBase>() != pickup.Base)
        {
            if (!InteractionRaycast.TryGetNearbyHit(player, ray, 3f, mask, out hit) || hit.collider.GetComponentInParent<ItemPickupBase>() != pickup.Base)
                return false;
        }

        Debug.DrawLine(ray.origin, hit.point, Colors.Blue * 50, 15);
        return true;
    }
}
