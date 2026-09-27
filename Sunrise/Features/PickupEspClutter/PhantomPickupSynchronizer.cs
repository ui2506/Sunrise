using LabApi.Features.Wrappers;
using System.Linq;
using MapGeneration;

namespace Sunrise.Features.PickupEspClutter;

internal static class PhantomPickupSynchronizer
{
    static readonly HashSet<RoomName> ExcludedRooms =
    [
        RoomName.Outside,

        RoomName.Hcz106,
        RoomName.HczAcroamaticAbatement,
        RoomName.HczWarhead,
        RoomName.HczMicroHID,

        RoomName.Lcz173,
    ];

    static List<Room>? _rooms;
    static List<Room> Rooms => _rooms ??= Room.List.Where(r => !ExcludedRooms.Contains(r.Name)
        && !r.GameObject.name.StartsWith("HCZ_Straight_PipeRoom")).ToList();

    static int index;

    internal static void Reset()
    {
        _rooms = null;
        index = 0;
    }

    internal static void GetNextPosition(out Vector3 position)
    {
        Room room = Rooms[index];

        const float RandomOffset = 1.5f; //todo increase
        position = room.Position + (Random.insideUnitSphere * RandomOffset) with { y = Random.Range(10, 15) };

        index = (index + 1) % Rooms.Count;
    }
}
