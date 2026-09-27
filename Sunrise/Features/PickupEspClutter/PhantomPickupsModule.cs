using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;

namespace Sunrise.Features.PickupEspClutter;

internal class PhantomPickupsModule : PluginModule
{
    public static readonly List<ItemType> PhantomItemTypes =
    [
        ItemType.SCP500,
        ItemType.Adrenaline,
        ItemType.ParticleDisruptor,
        ItemType.MicroHID,
        ItemType.Jailbird,
        ItemType.SCP1344,
    ];

    protected override void OnEnabled()
    {
        Handlers.ServerEvents.RoundStarted += OnRoundStarted;
        Handlers.ServerEvents.RoundEnded += OnRoundEnded;
        Handlers.PlayerEvents.PickingUpItem += OnPickingUpItem;
    }

    protected override void OnDisabled()
    {
        Handlers.ServerEvents.RoundStarted -= OnRoundStarted;
        Handlers.ServerEvents.RoundEnded -= OnRoundEnded;
        Handlers.PlayerEvents.PickingUpItem -= OnPickingUpItem;
    }

    protected override void OnReset()
    {
        PhantomItemSpawner.Stop();
        foreach (PhantomPickup pickup in new List<PhantomPickup>(PhantomPickup.List))
            pickup.Destroy();

        PhantomPickup.Pickups.Clear();
        PhantomPickupSynchronizer.Reset();
    }

    static void OnRoundStarted()
    {
        PhantomItemSpawner.Start();
    }

    static void OnRoundEnded(RoundEndedEventArgs ev)
    {
        PhantomItemSpawner.Stop();
    }

    static void OnPickingUpItem(PlayerPickingUpItemEventArgs ev)
    {
        if (PhantomPickup.Pickups.Contains(ev.Pickup))
            ev.IsAllowed = false;
    }
}
