using LabApi.Features.Wrappers;
using MapGeneration;

namespace Sunrise.API.Visibility.Generation;

internal static class VisibilityDataDebugVisualizer
{
    const float PrimitiveDuration = 15;

    static readonly Dictionary<Room, DebugData> RoomDebugData = new();

    internal static void Clear() => RoomDebugData.Clear();

    public static void DrawDebugPrimitives(VisibilityData data)
    {
        if (!RoomDebugData.TryGetValue(data.TargetRoom, out DebugData debugData))
        {
            debugData = new(data.TargetRoom);
            RoomDebugData[data.TargetRoom] = debugData;
        }

        if (debugData.IsReady())
        {
            debugData.ResetTime();

            VisualizeRoomConnections(data, debugData);
            VisualizeRoomAxes(data, debugData.Origin);
        }
    }

    static void VisualizeRoomConnections(VisibilityData data, DebugData debugData)
    {
        Debug.DrawPoint(debugData.Origin, debugData.Color, PrimitiveDuration);

        foreach (Vector3Int coord in data.VisibleCoords)
        {
            Debug.DrawLine(debugData.Origin, RoomUtils.CoordsToCenterPos(coord) + debugData.OriginOffset, debugData.Color, PrimitiveDuration);
        }
    }

    static void VisualizeRoomAxes(VisibilityData data, Vector3 origin)
    {
        Transform transform = data.TargetRoom.Transform;
        origin += Vector3.up * 0.05f;

        Debug.DrawLine(origin, origin + transform.right * 0.3f, Colors.Red * 50, PrimitiveDuration);
        Debug.DrawLine(origin, origin + transform.forward * 0.3f, Colors.Blue * 50, PrimitiveDuration);
        Debug.DrawLine(origin, origin + transform.up * 0.3f, Colors.Green * 50, PrimitiveDuration);
    }

    class DebugData(Room room)
    {
        public readonly Color Color = GetRandomColor();
        public readonly Vector3 OriginOffset = GetOriginOffset();
        public Vector3 Origin => room.Position + OriginOffset;

        float _lastPrimitiveSpawnTime = Time.time;

        static Color GetRandomColor()
            => Random.ColorHSV(0, 1, 0.7f, 1, 0.7f, 1) * 50;

        static Vector3 GetOriginOffset()
            => (Random.insideUnitSphere * 0.5f) with { y = Random.Range(0, 0.2f) } + Vector3.up * 0.3f;

        public void ResetTime() => _lastPrimitiveSpawnTime = Time.time;
        public bool IsReady() => Time.time > _lastPrimitiveSpawnTime + PrimitiveDuration;
    }
}
