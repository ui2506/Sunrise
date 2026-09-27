using LabApi.Features.Wrappers;
using System;
using MapGeneration;
using Sunrise.API.Visibility.Generation;

namespace Sunrise.API.Visibility;

public class VisibilityData
{
    static readonly Dictionary<Vector3Int, VisibilityData> Cache = new();

    VisibilityData(Room room)
    {
        TargetRoom = room ?? throw new ArgumentNullException(nameof(room));
        InitializeVisibilityData();
    }

    internal static void Clear() => Cache.Clear();

    public Room TargetRoom { get; }
    public HashSet<Vector3Int> VisibleCoords { get; } = [];

    public bool IsVisible(Player player) => player is not { IsDestroyed: false } || IsVisible(Room.GetRoomAtPosition(player.Position));
    public bool IsVisible(Room? room) => room?.Base is not RoomIdentifier identifier || VisibleCoords.Contains(identifier.MainCoords);

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
        Room? room = Room.GetRoomAtPosition(position);

        if (room is null)
            return null;

        return Get(room, allowDebug);
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