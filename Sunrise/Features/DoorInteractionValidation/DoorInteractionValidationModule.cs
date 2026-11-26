using Interactables;
using Interactables.Verification;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Enums;
using LabApi.Features.Wrappers;
using PlayerRoles.FirstPersonControl;
using Sunrise.API.Backtracking;

namespace Sunrise.Features.DoorInteractionValidation;

internal class AntiDoorManipulatorModule : PluginModule
{
    protected override void OnEnabled()
    {
        if (!Config.Instance.DoorInteractionValidation)
        {
            Debug.Log("DoorInteractionValidation module disabled according to the config!");
            return;
        }

        Handlers.PlayerEvents.InteractingDoor += OnInteractingDoor;
        Handlers.PlayerEvents.InteractingLocker += OnInteractingLocker;
        Handlers.PlayerEvents.InteractingElevator += OnInteractingElevator;

        base.OnEnabled();
    }

    protected override void OnDisabled()
    {
        Handlers.PlayerEvents.InteractingDoor -= OnInteractingDoor;
        Handlers.PlayerEvents.InteractingLocker -= OnInteractingLocker;
        Handlers.PlayerEvents.InteractingElevator -= OnInteractingElevator;

        base.OnDisabled();
    }

    static void OnInteractingDoor(PlayerInteractingDoorEventArgs ev)
    {
        if (!ev.IsAllowed || !ev.CanOpen)
            return;

        if (ev.Door.DoorName == DoorName.Lcz330Chamber || ev.Player.RoleBase is not IFpcRole || ev.Player.IsNoclipEnabled)
            return;

        if (CanInteract(ev.Player, ev.Door.Base))
            return;

        if (Config.Instance.Debug)
            ev.IsAllowed = false;
        else
            ev.CanOpen = false;
    }

    static void OnInteractingLocker(PlayerInteractingLockerEventArgs ev)
    {
        if (!ev.IsAllowed || ev.Player.RoleBase is not IFpcRole || ev.Player.IsNoclipEnabled)
            return;

        if (!CanInteract(ev.Player, ev.Locker.Base, ev.Chamber.Id))
            ev.IsAllowed = false;
    }

    static void OnInteractingElevator(PlayerInteractingElevatorEventArgs ev)
    {
        if (!ev.IsAllowed || ev.Player.RoleBase is not IFpcRole || ev.Player.IsNoclipEnabled)
            return;

        if (!CanInteract(ev.Player, ev.Panel.Target, ev.Panel.ColliderId))
            ev.IsAllowed = false;
    }

    static bool CanInteract(Player player, MonoBehaviour target, byte? colliderId = null)
    {
        const float maxDistance = StandardDistanceVerification.DefaultMaxDistance * 1.4f;

        Ray ray = new(player.Camera.position, BacktrackHistory.Get(player).LatestForward);

        int mask = InteractionCoordinator.RaycastMask & ~(int)(Mask.PlayerObjects | Mask.PlayerHitbox);

        bool isHitted = Physics.Raycast(ray, out RaycastHit hit, maxDistance, mask);

        InteractableCollider? collider = isHitted ? GetButton(hit) : null;

        bool isAllowed = collider != null && collider.Target == target && (!colliderId.HasValue || collider.ColliderId == colliderId);

        if (!isAllowed && InteractionRaycast.TryGetNearbyHit(player, ray, maxDistance, mask, out RaycastHit nearby))
        {
            hit = nearby;
            isHitted = true;
            collider = GetButton(hit);
            isAllowed = collider != null && collider.Target == target && (!colliderId.HasValue || collider.ColliderId == colliderId);

            Debug.DrawPoint(hit.point, (isAllowed ? Colors.Green : Colors.Red) * 50, 5f);
        }

        if (Config.Instance.DebugPrimitives)
        {
            Vector3 end = isHitted ? hit.point : ray.GetPoint(maxDistance);
            Color color = (isAllowed ? Colors.Green : Colors.Red) * 50;

            Debug.DrawLine(ray.origin, end, color, 5f);
        }

        if (isAllowed)
            return true;

        if (Config.Instance.Debug)
        {
            string actual = isHitted ? $"{hit.collider.name} at {hit.distance:F2} m, target {collider?.Target}, ID {collider?.ColliderId}" : $"no hit within {maxDistance:F2} m";
            Debug.Log($"Interaction blocked for {player.Nickname}. Expected {target}, ID {colliderId?.ToString() ?? "any"}. Ray hit: {actual}.");
        }

        return false;
    }

    static InteractableCollider? GetButton(RaycastHit hit)
    {
        if (!hit.collider.TryGetComponent(out InteractableCollider button) && hit.collider.transform.parent is Transform parent)
            parent.TryGetComponent(out button);

        return button;
    }
}
