using HarmonyLib;
using InventorySystem.Items.Firearms.Modules.Misc;
using JetBrains.Annotations;
using LabApi.Features.Wrappers;
using RelativePositioning;
using Sunrise.API.Backtracking;
using System;
using BaseFirearm = InventorySystem.Items.Firearms.Firearm;

namespace Sunrise.Features.ServersideBacktrack;

[HarmonyPatch(typeof(ShotBacktrackData), nameof(ShotBacktrackData.ProcessShot)), UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] 
internal static class BacktrackOverridePatch
{
    static readonly AutoBenchmark Benchmark = new("Server-Side Backtrack");

    static bool Prefix(BaseFirearm firearm, Action<ReferenceHub> processingMethod, ShotBacktrackData __instance)
    {
        if (!Config.Instance.ServersideBacktrack)
            return true;

        Benchmark.Start();

        try
        {
            ProcessShot(firearm, processingMethod, __instance);
            return false;
        }
        finally
        {
            Benchmark.Increment();
            Benchmark.Stop();
        }
    }

    static void ProcessShot(BaseFirearm firearm, Action<ReferenceHub> processingMethod, ShotBacktrackData backtrackData)
    {
        if (!WaypointBase.TryGetWaypoint(backtrackData.RelativeOwnerPosition.WaypointId, out WaypointBase wp))
            return;

        Player player = Player.Get(firearm.Owner);
        Vector3 worldspacePosition = wp.GetWorldspacePosition(backtrackData.RelativeOwnerPosition.Relative);
        Quaternion worldspaceRotation = wp.GetWorldspaceRotation(backtrackData.RelativeOwnerRotation);
        BacktrackEntry ownerClaimed = new(worldspacePosition, worldspaceRotation);

        if (Config.Instance.Debug) // The red line shows the claimed position
        {
            BacktrackEntry prev = new(player);
            ownerClaimed.Restore(player);
            Debug.DrawLine(player.Camera.position, player.Camera.position + player.Camera.forward * 100f, Colors.Red * 50, 15);
            prev.Restore(player);
        }

        using BacktrackProcessor attackerProcessor = new(player, ownerClaimed, true);

        // The green line shows the found position. For normal players they should match most of the time.
        Debug.DrawLine(player.Camera.position, player.Camera.position + player.Camera.forward * 100f, Colors.Green * 50, 15);

        if (backtrackData.HasPrimaryTarget)
        {
            Player target = Player.Get(backtrackData.PrimaryTargetHub);
            BacktrackEntry targetClaimed = new(backtrackData.PrimaryTargetRelativePosition.Position, Quaternion.identity);

            using BacktrackProcessor targetProcessor = new(target, targetClaimed, false);

            processingMethod(backtrackData.PrimaryTargetHub);
        }
        else
        {
            processingMethod(null!);
        }
    }
}