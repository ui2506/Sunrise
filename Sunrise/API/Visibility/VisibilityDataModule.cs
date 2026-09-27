using LabApi.Features.Wrappers;
using System;
using MapGeneration;
using Sunrise.API.Visibility.Generation;

namespace Sunrise.API.Visibility;

internal class VisibilityModule : PluginModule
{
    protected override void OnEnabled()
    {
        SeedSynchronizer.OnGenerationStage += OnMapGenerationStage;
    }

    protected override void OnDisabled()
    {
        SeedSynchronizer.OnGenerationStage -= OnMapGenerationStage;
    }

    protected override void OnReset()
    {
        VisibilityData.Clear();
        VisibilityDataDebugVisualizer.Clear();
    }

    static void OnMapGenerationStage(MapGenerationPhase mapGenerationStage)
    {
        if (mapGenerationStage == MapGenerationPhase.RelativePositioningWaypoints)
        {
            VisibilityData.Clear();
            VisibilityDataDebugVisualizer.Clear();
            foreach (Room room in Room.List)
            {
                try
                {
                    VisibilityData.Get(room);
                }
                catch (Exception e)
                {
                    Log.Error($"Failed to Get visibility data for room {room.Name} during map generation: {e}");
                }
            }
        }
    }
}
