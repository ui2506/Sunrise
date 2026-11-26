using LabApi.Events.Arguments.PlayerEvents;
using NorthwoodLib.Pools;
using Sunrise.API.Backtracking;
using Sunrise.API.Detections;
using Sunrise.API.Visibility;
using Sunrise.Features.AntiBigStep;
using Sunrise.Features.AntiNoRecoil;
using Sunrise.Features.AntiWallHack;
using Sunrise.Features.DoorInteractionValidation;
using Sunrise.Features.ExploitPatches;
using Sunrise.Features.PickupValidation;
using Sunrise.Features.ServersideTeslaDamage;
using System;
using System.Text;

namespace Sunrise.Utility;

public abstract class PluginModule
{
    public static List<PluginModule> Modules { get; } =
    [
        // API
        new DetectionHandler(),
        new BacktrackingModule(),
        new VisibilityModule(),

        // Features
        new AntiWallHackModule(),
        new AntiBigStepModule(),
        new AntiNoRecoilModule(),
        new PickupValidationModule(),
        new ServersideTeslaDamageModule(),
        new AntiDoorManipulatorModule(),
        new ExploitPatchesLoader(),
    ];

    private static Version? _version;

    protected virtual List<PluginModule> SubModules { get; } = [];

    public bool IsEnabled { get; private set; }

    public static void EnableAll(Version version)
    {
        foreach (PluginModule module in Modules)
            module.Enable();

        Handlers.PlayerEvents.Joined += OnJoined;

        _version = version;
    }

    public static void DisableAll()
    {
        foreach (PluginModule module in Modules)
            module.Disable();

        Handlers.PlayerEvents.Joined -= OnJoined;
    }

    static void OnJoined(PlayerJoinedEventArgs ev)
    {
        StringBuilder builder = StringBuilderPool.Shared.Rent();
        builder.AppendLine($"Sunrise {_version}");
        builder.AppendLine($"Active modules:");

        foreach (PluginModule module in Modules)
        {
            if (!module.IsEnabled)
                continue;

            builder.AppendLine(module.GetType().Name);

            foreach (PluginModule submodule in module.SubModules)
                builder.AppendLine(submodule.GetType().Name);
        }

        ev.Player.SendConsoleMessage(builder.ToString());

        StringBuilderPool.Shared.Return(builder);
    }

    public void Enable()
    {
        if (IsEnabled)
            return;

        OnEnabled();

        Handlers.ServerEvents.WaitingForPlayers += OnReset;

        foreach (PluginModule module in SubModules)
            module.Enable();
    }

    public void Disable()
    {
        if (!IsEnabled)
            return;

        OnDisabled();

        Handlers.ServerEvents.WaitingForPlayers -= OnReset;

        foreach (PluginModule module in SubModules)
            module.Disable();

        OnReset();
    }

    protected virtual void OnEnabled()
    {
        IsEnabled = true;
    }

    protected virtual void OnDisabled()
    {
        IsEnabled = false;
    }

    protected virtual void OnReset() { }
}
