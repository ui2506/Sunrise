using Sunrise.API.Backtracking;
using Sunrise.API.Detections;
using Sunrise.API.Visibility;
using Sunrise.Features.AntiWallhack;
using Sunrise.Features.AntiBigStep;
using Sunrise.Features.DoorInteractionValidation;
using Sunrise.Features.ExploitPatches;
using Sunrise.Features.PickupValidation;
using Sunrise.Features.ServersideTeslaDamage;

namespace Sunrise.EntryPoint;

public sealed class SunriseLoader : PluginModule
{
    protected override List<PluginModule> SubModules { get; } =
    [
        // API
        new DetectionHandler(),
        new BacktrackingModule(),
        new VisibilityModule(),

        // Features
        new AntiWallhackModule(),
        new AntiBigStepModule(),
        new PickupValidationModule(),
        new ServersideTeslaDamageModule(),
        new AntiDoorManipulatorModule(),
        new ExploitPatchesLoader(),
        // new PhantomPickupsModule(), // BUG: Flickering
    ];
}
