using AdminToys;
using LabApi.Features.Wrappers;
using MEC;
using System.Diagnostics;
using PrimitiveObjectToy = LabApi.Features.Wrappers.PrimitiveObjectToy;

namespace Sunrise.Utility;

internal static class Debug
{
    [Conditional("DEBUG")]
    internal static void DrawCube(Vector3 position, Vector3 scale, Color color = default, float duration = 10f)
    {
        if (!Config.Instance.DebugPrimitives)
            return;

        color = GetColor(color);

        var cube = PrimitiveObjectToy.Create(networkSpawn: false);
        cube.Type = PrimitiveType.Cube;
        cube.Flags = PrimitiveFlags.Visible;
        cube.Position = position;
        cube.Scale = scale;
        cube.Color = color;
        cube.Spawn();

        Timing.CallDelayed(duration, cube.Destroy);
    }

    [Conditional("DEBUG")]
    internal static void DrawLine(Vector3 start, Vector3 end, Color color = default, float duration = 10f)
    {
        if (!Config.Instance.DebugPrimitives)
            return;

        color = GetColor(color);

        GetLineData(start, end, 0.01f, false, out Vector3 position, out Vector3 scale, out Quaternion rotation);

        var line = PrimitiveObjectToy.Create(networkSpawn: false);
        line.Type = PrimitiveType.Cylinder;
        line.Flags = PrimitiveFlags.Visible;
        line.Position = position;
        line.Rotation = rotation;
        line.Scale = scale;
        line.Color = color;
        line.Spawn();

        Timing.CallDelayed(duration, line.Destroy);
    }

    [Conditional("DEBUG")]
    internal static void DrawPoint(Vector3 position, Color color = default, float duration = 10f)
    {
        if (!Config.Instance.DebugPrimitives)
            return;

        color = GetColor(color);

        var point = PrimitiveObjectToy.Create(networkSpawn: false);
        point.Type = PrimitiveType.Sphere;
        point.Flags = PrimitiveFlags.Visible;
        point.Position = position;
        point.Scale = Vector3.one * 0.1f;
        point.Color = color;
        point.Spawn();
        Timing.CallDelayed(duration, point.Destroy);
    }

    [Conditional("DEBUG")]
    internal static void Log(string s) => LabApi.Features.Console.Logger.Debug(s, Config.Instance.Debug);

    static void GetLineData(Vector3 from, Vector3 to, float thickness, bool cube, out Vector3 position, out Vector3 scale, out Quaternion rotation)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        scale = new Vector3(thickness, distance * (cube ? 1 : 0.5f), thickness);
        position = from + direction * 0.5f;
        rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90, 0, 0);
    }

    static Color GetColor(Color color)
    {
        if (color == default)
            color = Color.red;

        if (color.a > 1)
            color.a = 0.1f;

        return color;
    }
}