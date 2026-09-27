using LabApi.Features.Wrappers;
using JetBrains.Annotations;
using MEC;
using Mirror;

using Sunrise.API.Visibility;

namespace Sunrise.Features.PickupEspClutter;

internal class PhantomPickup : MonoBehaviour
{
    readonly HashSet<Player> _hiddenFor = new();
    CoroutineHandle _coroutine;
    NetworkIdentity _netIdentity = null!;

    LinkedListNode<PhantomPickup> _node = null!;
    Pickup _pickup = null!;

    [UsedImplicitly] public static bool DebugMode { get; set; }

    internal static LinkedList<PhantomPickup> List { get; } = [];
    internal static HashSet<Pickup> Pickups { get; } = [];

    void Start()
    {
        _node = List.AddLast(this);

        _pickup = Pickup.Get(GetComponent<InventorySystem.Items.Pickups.ItemPickupBase>());
        Pickups.Add(_pickup);
        _netIdentity = _pickup.Base.netIdentity;

        _coroutine = Timing.RunCoroutine(Coroutine());
    }

    void OnDestroy()
    {
        List.Remove(_node);
        Pickups.Remove(_pickup);

        _hiddenFor.Clear();

        Timing.KillCoroutines(_coroutine);
    }

    public void Destroy()
    {
        _pickup.Destroy();
    }

    IEnumerator<float> Coroutine()
    {
        while (true)
        {
            HideForEveryone();

            yield return Timing.WaitForSeconds(Random.Range(0.4f, 0.5f));

            // Choose a new position for the item
            PhantomPickupSynchronizer.GetNextPosition(out Vector3 position);
            _pickup.Position = position;

            // Wait for the item to change position to one where it wont be noticed by legit players immediately, so we can safely update visibility
            yield return Timing.WaitForSeconds(Random.Range(0.4f, 0.5f));

            // Lay on the ground for some time
            float idleTime = Random.Range(5f, 15f);

            while (idleTime > 0)
            {
                // Update visibility for all players
                UpdateVisibility();

                float waitTime = Random.Range(1f, 2f);
                idleTime -= waitTime;

                yield return Timing.WaitForSeconds(waitTime);
            }

            /*
            Debug.Log($"{cycleCount} PhantomPickup cycles took {SW.Elapsed.TotalMilliseconds:F5}ms");
            Tested with 30 players and 200 phantom pickups
            [2025-03-25 19:19:00.027 +02:00] [DEBUG] [Sunrise] 271 PhantomPickup cycles took 93.61230ms
            [2025-03-25 19:19:29.977 +02:00] [DEBUG] [Sunrise] 800 PhantomPickup cycles took 212.33240ms
            in ~30 seconds, 529 cycles took 118.7201ms. This means the average MSPT is (1000/60)*(118.72/30000) = 0.065955
            */
        }
    }

    void HideForEveryone()
    {
        foreach (Player player in Player.Dictionary.Values)
            SetVisibility(player, false);
    }

    void UpdateVisibility()
    {
        VisibilityData? visibilityData = VisibilityData.Get(transform.position, false);

        if (visibilityData == null)
        {
            HideForEveryone();
            return;
        }

        foreach (Player player in Player.Dictionary.Values)
            SetVisibility(player, !IsObserving(player, visibilityData));
    }

    void SetVisibility(Player player, bool visibility)
    {
        switch (visibility)
        {
            case true when _hiddenFor.Remove(player):
                NetworkServer.ShowForConnection(_netIdentity, player.Connection);
                break;
            case false when _hiddenFor.Add(player):
                NetworkServer.HideForConnection(_netIdentity, player.Connection);
                break;
        }
    }

    // Whether the player will be able to see the item
    static bool IsObserving(Player player, VisibilityData visibilityData)
    {
        if (DebugMode)
            return MathExtensions.SqrDistance(player.Position, visibilityData.TargetRoom.Position) < 1.5f * 1.5f;

        return visibilityData.IsVisible(player);
    }

    internal static void DestroyOldest()
    {
        if (List.Count > 0)
            List.First.Value.Destroy();
    }

    internal static void Create()
    {
        PhantomPickupSynchronizer.GetNextPosition(out Vector3 position);
        ItemType type = PhantomPickupsModule.PhantomItemTypes[Random.Range(0, PhantomPickupsModule.PhantomItemTypes.Count)];
        var pickup = Pickup.Create(type, position, Random.rotation);
        pickup?.GameObject.AddComponent<PhantomPickup>();
    }
}
