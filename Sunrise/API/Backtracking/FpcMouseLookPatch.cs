using HarmonyLib;
using JetBrains.Annotations;
using PlayerRoles.FirstPersonControl;
using RelativePositioning;

namespace Sunrise.API.Backtracking;

[HarmonyPatch(typeof(FpcMouseLook), nameof(FpcMouseLook.ApplySyncValues)), UsedImplicitly]
internal static class FpcMouseLookPatch
{
    static readonly AutoBenchmark Benchmark = new("FpcMouseLook.ApplySyncValues (record history)");

    [UsedImplicitly]
    static void Prefix(ushort horizontal, ushort vertical, FpcMouseLook __instance)
    {
        using var measurement = Benchmark.Start();

        Quaternion localRotation = ConvertToQuaternion(horizontal, vertical);
        Quaternion worldRotation = WaypointBase.GetWorldRotation(__instance._fpmm.Motor.ReceivedPosition.WaypointId, localRotation);
        BacktrackHistory.Get(__instance._hub).RecordEntry(__instance._hub.transform.position, worldRotation);
    }

    static Quaternion ConvertToQuaternion(ushort horizontal, ushort vertical)
    {
        const float ushortToFloat = 1f / ushort.MaxValue;

        float hAngle = Mathf.Lerp(0f, 360f, horizontal * ushortToFloat);
        float vAngle = Mathf.Lerp(88f, -88f, vertical * ushortToFloat);

        return Quaternion.Euler(vAngle, hAngle, 0f);
    }
}