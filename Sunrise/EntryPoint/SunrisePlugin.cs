using HarmonyLib;
using LabApi.Loader.Features.Plugins;
using System;

namespace Sunrise.EntryPoint;

public sealed class SunrisePlugin : Plugin<Config>
{
    public override string Name { get; } = "Sunrise";
    public override string Author { get; } = "BanalnyBanan (Updated and ported by ui_2506)";
    public override Version Version { get; } = new(2, 0, 0);
    public override string Description { get; } = "AntiCheat";
    public override Version RequiredApiVersion { get; } = new(1, 1, 7);

    public SunriseLoader Loader { get; } = new();
    public Harmony Harmony { get; } = new("Sunrise");

    public override void Enable()
    {
        Loader.Enable();
        Harmony.PatchAll();
    }

    public override void Disable()
    {
        Loader.Disable();
        Harmony.UnpatchAll(Harmony.Id);
    }
}
