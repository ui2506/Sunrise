using LabApi.Events.Arguments.Scp914Events;

namespace Sunrise.Features.PickupValidation;

internal class PickupValidationModule : PluginModule
{
    protected override void OnEnabled()
    {
        if (!Config.Instance.PickupValidation)
        {
            Debug.Log("PickupValidation module disabled according to the config!");
            return;
        }

        Handlers.Scp914Events.ProcessingInventoryItem += OnScp914ProcessingInventoryItem;
        Handlers.PlayerEvents.SearchingPickup += PickupValidator.OnSearchingPickup;
        Handlers.PlayerEvents.PickingUpItem += PickupValidator.OnPickingUp;
        Handlers.PlayerEvents.PickingUpAmmo += PickupValidator.OnPickingUp;
        Handlers.PlayerEvents.PickingUpArmor += PickupValidator.OnPickingUp;
        Handlers.PlayerEvents.PickingUpScp330 += PickupValidator.OnPickingUp;
    }

    protected override void OnDisabled()
    {
        Handlers.Scp914Events.ProcessingInventoryItem -= OnScp914ProcessingInventoryItem;
        Handlers.PlayerEvents.SearchingPickup -= PickupValidator.OnSearchingPickup;
        Handlers.PlayerEvents.PickingUpItem -= PickupValidator.OnPickingUp;
        Handlers.PlayerEvents.PickingUpAmmo -= PickupValidator.OnPickingUp;
        Handlers.PlayerEvents.PickingUpArmor -= PickupValidator.OnPickingUp;
        Handlers.PlayerEvents.PickingUpScp330 -= PickupValidator.OnPickingUp;
    }

    protected override void OnReset() => PickupValidator.Clear();

    static void OnScp914ProcessingInventoryItem(Scp914ProcessingInventoryItemEventArgs ev)
    {
        PickupValidator.TemporaryPlayerBypass[ev.Player] = Time.time + 0.01f;
    }
}
