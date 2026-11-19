using HarmonyLib;
using PlayerRoles.PlayableScps.Scp096;
using Sunrise.API.Backtracking;

namespace Sunrise.Features.ServersideBacktrack;

/// <summary>
/// It’s not working for now
/// </summary>

[HarmonyPatch(typeof(Scp096TargetsTracker), nameof(Scp096TargetsTracker.IsObservedBy))]
internal static class Scp096TrackerPatch
{
    static void Prefix(ReferenceHub __0, out Quaternion? __state)
    {
        __state = null;

        if (!BacktrackHistory.Dictionary.TryGetValue(__0, out BacktrackHistory history) || history.Entries.IsEmpty)
            return;

        Transform camera = __0.PlayerCameraReference;

        __state = camera.rotation;

        camera.rotation = history.Entries.Front().Rotation;
    }

    static void Finalizer(ReferenceHub __0, Quaternion? __state)
    {
        if (__state.HasValue)
            __0.PlayerCameraReference.rotation = __state.Value;
    }
}
