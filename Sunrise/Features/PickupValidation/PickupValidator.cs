using InventorySystem.Items.Pickups;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using Sunrise.API.Backtracking;

namespace Sunrise.Features.PickupValidation;

internal static class PickupValidator
{
    internal static readonly Dictionary<Player, float> TemporaryPlayerBypass = [];

    internal static void OnSearchingPickup(PlayerSearchingPickupEventArgs ev)
    {
        if (!Config.Instance.PickupValidation || !ev.IsAllowed || ev.Player.IsNoclipEnabled)
            return;

        if (TemporaryPlayerBypass.TryGetValue(ev.Player, out float time) && time > Time.time)
            return;

        //foreach (Elevator elevator in Elevator.List)
        //{
        //    if (elevator.WorldSpaceRelativeBounds.Contains(ev.Player.Position) && (uint)(elevator.Base.CurSequence - 2) <= 1u)
        //        return;
        //}

        if (ev.Pickup is BodyArmorPickup)
            return;

        if (!CanPickUp(ev.Player, ev.Pickup))
            ev.IsAllowed = false;
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
