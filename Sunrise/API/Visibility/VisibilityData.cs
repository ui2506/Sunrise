using LabApi.Features.Wrappers;
using System;
using MapGeneration;
using PlayerRoles.PlayableScps;
using Sunrise.API.Visibility.Generation;

namespace Sunrise.API.Visibility;

public class VisibilityData
{
    static readonly Dictionary<Vector3Int, VisibilityData> Cache = [];
    static readonly Dictionary<Vector3, Room?> Rooms = [];
    static int roomFrame = -1;

    VisibilityData(Room room)
    {
        TargetRoom = room ?? throw new ArgumentNullException(nameof(room));
        InitializeVisibilityData();
    }

    internal static void Clear()
    {
        Cache.Clear();
        Rooms.Clear();
    }

    public Room TargetRoom { get; }
    public HashSet<Vector3Int> VisibleCoords { get; } = [];

    public bool IsVisible(Player player) => player is not { IsDestroyed: false } || IsVisible(GetRoom(player.Position));
    public bool IsVisible(Room? room) => room?.Base is not RoomIdentifier identifier || VisibleCoords.Contains(identifier.MainCoords);

    internal static float GetRangeSquared(Vector3 observerPosition) => GetRoom(observerPosition)?.Zone == FacilityZone.Surface ? 50f * 50f : 30f * 30f;

    internal static bool InRange(Vector3 observerPosition, float sqrDistance) => sqrDistance <= 30f * 30f
        || (sqrDistance <= 50f * 50f && sqrDistance <= GetRangeSquared(observerPosition));

    internal static int GetVisionMask(Room? observer, Room? target)
    {
        int mask = VisionInformation.VisionLayerMask;

        if (observer != null && target != null && (observer == target || observer.ConnectedRooms.Contains(target.Base)))
            mask &= ~(int)Mask.Doors;

        return mask;
    }

    void InitializeVisibilityData()
    {
        VisibilityGenerator.AddRoomAndNeighbors(VisibleCoords, TargetRoom);
        Debug.Log($"Generated visibility data for room {TargetRoom.Name}. Total visible coords: {VisibleCoords.Count}");
    }

    public static VisibilityData? Get(Player player, bool allowDebug = true)
    {
        if (player is null)
            throw new ArgumentNullException(nameof(player));

        return Get(player.Position, allowDebug);
    }

    public static VisibilityData? Get(Vector3 position, bool allowDebug = true)
    {
        Room? room = GetRoom(position);

        if (room is null)
            return null;

        return Get(room, allowDebug);
    }

    internal static Room? GetRoom(Vector3 position)
    {
        int frame = Time.frameCount;
        if (roomFrame != frame)
        {
            Rooms.Clear();
            roomFrame = frame;
        }

        if (!Rooms.TryGetValue(position, out Room? room))
            Rooms[position] = room = Room.GetRoomAtPosition(position);

        return room;
    }

    public static VisibilityData Get(Room room, bool allowDebug = true)
    {
        if (room is null)
            throw new ArgumentNullException(nameof(room));

        Vector3Int coords = room.Base.MainCoords;

        if (!Cache.TryGetValue(coords, out VisibilityData? data))
        {
            data = new(room);
            Cache[room.Base.MainCoords] = data;
        }

        if (Config.Instance.DebugPrimitives && allowDebug)
            VisibilityDataDebugVisualizer.DrawDebugPrimitives(data);

        return data;
    }
}
