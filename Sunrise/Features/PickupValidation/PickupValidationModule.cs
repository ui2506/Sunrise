using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp914Events;

namespace Sunrise.Features.PickupValidation;

internal class PickupValidationModule : PluginModule
{
    protected override void OnEnabled()
    {
        Handlers.Scp914Events.ProcessingInventoryItem += OnScp914ProcessingInventoryItem;
        Handlers.PlayerEvents.InteractingLocker += OnInteractingLocker;
        Handlers.PlayerEvents.InteractingDoor += OnInteractingDoor;

        Handlers.PlayerEvents.PickingUpItem += PickupValidator.OnPickingUpItem;
        Handlers.PlayerEvents.PickingUpAmmo += PickupValidator.OnPickingUpItem;
        Handlers.PlayerEvents.PickingUpScp330 += PickupValidator.OnPickingUpItem;
    }

    protected override void OnDisabled()
    {
        Handlers.Scp914Events.ProcessingInventoryItem -= OnScp914ProcessingInventoryItem;
        Handlers.PlayerEvents.InteractingLocker -= OnInteractingLocker;
        Handlers.PlayerEvents.InteractingDoor -= OnInteractingDoor;

        Handlers.PlayerEvents.PickingUpItem -= PickupValidator.OnPickingUpItem;
        Handlers.PlayerEvents.PickingUpAmmo -= PickupValidator.OnPickingUpItem;
        Handlers.PlayerEvents.PickingUpScp330 -= PickupValidator.OnPickingUpItem;
    }

    protected override void OnReset()
    {
        PickupValidator.TemporaryPlayerBypass.Clear();
        PickupValidator.LockerLastInteraction.Clear();
        PickupValidator.DoorLastInteraction.Clear();
    }

    static void OnScp914ProcessingInventoryItem(Scp914ProcessingInventoryItemEventArgs ev)
    {
        PickupValidator.TemporaryPlayerBypass[ev.Player] = Time.time + 0.01f;
    }

    static void OnInteractingLocker(PlayerInteractingLockerEventArgs ev)
    {
        if (ev.IsAllowed)
            PickupValidator.LockerLastInteraction[ev.Chamber.Base] = Time.time;
    }

    static void OnInteractingDoor(PlayerInteractingDoorEventArgs ev)
    {
        if (ev.IsAllowed)
            PickupValidator.DoorLastInteraction[ev.Door.Base] = Time.time;
    }
}
