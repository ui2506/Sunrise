using System;
using AdminToys;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;
using LockerChamber = MapGeneration.Distributors.LockerChamber;

using LabApi.Features.Wrappers;

using LabApi.Events.Arguments.Interfaces;
using Interactables.Interobjects.DoorUtils;
using InventorySystem.Items.Pickups;
using JetBrains.Annotations;
using MapGeneration.Distributors;
using Sunrise.API.Backtracking;

namespace Sunrise.Features.PickupValidation;

internal static class PickupValidator
{
    internal static readonly Dictionary<Player, float> TemporaryPlayerBypass = new();
    internal static readonly Dictionary<LockerChamber, float> LockerLastInteraction = new();
    internal static readonly Dictionary<DoorVariant, float> DoorLastInteraction = new();

    static readonly RaycastHit[] HitBuffer = new RaycastHit[32];

    [UsedImplicitly] public static bool AlwaysBlock { get; set; }

    internal static void OnPickingUpItem<T>(T ev) where T : IPlayerEvent, IPickupEvent, ICancellableEvent
    {
        if (!Config.Instance.PickupValidation || ev.Player is null || ev.Pickup is null || !ev.Pickup.Base || ev.Player.IsNoclipEnabled)
            return;

        if (TemporaryPlayerBypass.TryGetValue(ev.Player, out float time) && time > Time.time)
            return;

        if (ev.Pickup is BodyArmorPickup)
            return;

        if (AlwaysBlock || !CanPickUp(ev.Player, ev.Pickup))
        {
            ev.IsAllowed = false;
        }
    }

    static bool CanPickUp(Player player, Pickup pickup)
    {
        float bypassTime = pickup.Base.SearchTimeForPlayer(player.ReferenceHub) + 1f;

        if (!IsObstructed(player.Camera.position, pickup.Position, out _, bypassTime))
            return true;

        Bounds bounds = pickup.Base.GetComponentInChildren<Renderer>().bounds;
        Vector3 eyePos = player.Camera.position;
        Vector3 direction = BacktrackHistory.Get(player).LatestForward;

        if (CanPickUpDirect(eyePos, direction, pickup))
            return true;

        bool result = CanPickupBounds(eyePos, bounds, bypassTime);

        if (!result)
        {
            Vector3 jumpEyePos = GetJumpEyePos(eyePos);

            result = CanPickupBounds(jumpEyePos, bounds, bypassTime);

            if (!result && Config.Instance.DebugPrimitives)
            {
                foreach (Vector3 point in GetCorners(bounds))
                {
                    Debug.DrawLine(eyePos, point, Colors.Red * 30, 10f);
                }

                foreach (Vector3 point in GetCorners(bounds))
                {
                    Debug.DrawLine(jumpEyePos, point, Colors.Red * 30, 10f);
                }
            }
        }

        Debug.DrawCube(bounds.center, bounds.size, result ? Colors.Green * 30 : Colors.Red * 30, 10f);

        return result;
    }

    static bool CanPickUpDirect(Vector3 eyePos, Vector3 direction, Pickup pickup)
    {
        var ray = new Ray(eyePos, direction);

        if (!Physics.Raycast(ray, out RaycastHit pickupHit, 3, (int)(Mask.HitregObstacles | Mask.Pickups)))
            return false;

        if (pickupHit.collider.gameObject.layer != (int)Layer.Pickups)
            return false;

        if (pickupHit.collider.GetComponentInParent<ItemPickupBase>() != pickup.Base)
            return false;

        Debug.DrawLine(ray.origin, pickupHit.point, Colors.Blue * 50, 15);
        return true;
    }

    static bool CanPickupBounds(Vector3 position, Bounds bounds, float bypassTime)
    {
        foreach (Vector3 corner in GetCorners(bounds))
        {
            if (IsObstructed(position, corner, out RaycastHit hit, bypassTime))
            {
                Debug.Log($"Hit {hit.collider?.gameObject?.name} ({hit.collider?.gameObject?.layer:G}) at {hit.point}");
                continue;
            }

            Debug.DrawLine(position, corner, Colors.Green * 50, 10f);
            return true;
        }

        return false;
    }

    static Vector3 GetJumpEyePos(Vector3 eyePos)
    {
        var upRay = new Ray(eyePos, Vector3.up);
        var jumpHeight = 0.75f;

        if (Physics.Raycast(upRay, out RaycastHit hit, jumpHeight, (int)Mask.PlayerObstacles))
            jumpHeight = hit.distance - 0.05f;

        return upRay.GetPoint(jumpHeight);
    }

    static IEnumerable<Vector3> GetCorners(Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        yield return center;

        yield return center + new Vector3(extents.x, extents.y, extents.z);
        yield return center + new Vector3(extents.x, extents.y, -extents.z);
        yield return center + new Vector3(extents.x, -extents.y, extents.z);
        yield return center + new Vector3(extents.x, -extents.y, -extents.z);

        yield return center + new Vector3(-extents.x, extents.y, extents.z);
        yield return center + new Vector3(-extents.x, extents.y, -extents.z);
        yield return center + new Vector3(-extents.x, -extents.y, extents.z);
        yield return center + new Vector3(-extents.x, -extents.y, -extents.z);
    }

    static bool IsObstructed(Vector3 a, Vector3 b, out RaycastHit hit, float bypassTime) //todo test
    {
        var ray = new Ray(a, b - a);
        int count = Physics.RaycastNonAlloc(ray, HitBuffer, Vector3.Distance(a, b), (int)Mask.HitregObstacles);

        // Sort hits by distance
        Array.Sort(HitBuffer, 0, count, RaycastHitComparer.Instance);

        for (var i = 0; i < count; i++)
        {
            hit = HitBuffer[i];

            // Pass through ignored colliders
            switch ((Layer)hit.collider.gameObject.layer)
            {
                // Moving doors 
                case Layer.Doors when hit.collider.gameObject.GetComponentInParent<DoorVariant>() is DoorVariant door && DoorLastInteraction.TryGetValue(door, out float time) && time + bypassTime > Time.time:

                // Moving SCP pedestal doors
                case Layer.Glass when hit.collider.GetComponentInParent<LockerChamber>() is LockerChamber locker && LockerLastInteraction.TryGetValue(locker, out time) && time + bypassTime > Time.time:

                // Primitives with collision disabled
                case Layer.DefaultColliders when hit.collider.GetComponentInParent<PrimitiveObjectToy>() is PrimitiveObjectToy toy && (toy.NetworkPrimitiveFlags & PrimitiveFlags.Collidable) == 0:
                {
                    Debug.DrawPoint(hit.point, Colors.Green * 50, 10f);
                    continue;
                }
            }

            // Obstruction found
            return true;
        }

        // No obstructions
        hit = default;
        return false;
    }

    class RaycastHitComparer : IComparer<RaycastHit>
    {
        public static readonly RaycastHitComparer Instance = new();
        public int Compare(RaycastHit hitA, RaycastHit hitB) => hitA.distance.CompareTo(hitB.distance);
    }
}
