using HarmonyLib;
using LabApi.Loader.Features.Plugins;
using Sunrise.Features.ServerList;
using System;

namespace Sunrise.EntryPoint;

public sealed class SunrisePlugin : Plugin<Config>
{
    public override string Name { get; } = "Sunrise";
    public override string Author { get; } = "ui_2506 (The author of the first Exiled versions is BanalnyBanan)";
    public override Version Version { get; } = new(2, 6, 0);
    public override string Description { get; } = "AntiCheat";
    public override Version RequiredApiVersion { get; } = LabApi.Features.LabApiProperties.CurrentVersion;

    public static Harmony Harmony { get; } = new("Sunrise");

    public override void Enable()
    {
        PluginModule.EnableAll(Version);

        ServerListNamePatch.Tag = $" <color=#00000000><size=1>Sunrise {Version}</size></color>";
        Harmony.CreateClassProcessor(typeof(ServerListNamePatch)).Patch();
    }

    public override void Disable()
    {
        PluginModule.DisableAll();

        Harmony.UnpatchAll(Harmony.Id);
    }
}
