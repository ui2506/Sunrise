using Sunrise.API.Detections;
using System.ComponentModel;

namespace Sunrise.EntryPoint;

public sealed class Config
{
    public bool Debug { get; set; } = false;

    [Description("Enables some visual debugging features.")]
    public bool DebugPrimitives { get; set; } = false;

    [Description("Enables benchmarks to measure performance impact of various features on your server performance.")]
    public bool Benchmark { get; set; } = false;

    [Description("The maximum latency for which the server has to account. Higher values give more authority to clients, lower values may decrease gameplay quality for players with higher latency.")]
    public float AccountedLatencySeconds { get; set; } = 0.3f;

    [Description("Toggle features separately: Significantly reduces wallhack usefulness using per-room visibility maps. Performance impact negligible.")]
    public bool AntiWallhack { get; set; } = true;

    [Description(
        "Completely cuts off wallhack past the sound range using precise line-of-sight checks. " +
        "Performance impact possible: ~1.3% of server time consumed with 30 players in proximity. " +
        "May cause issues with custom maps if transparent objects are using the default layer (0) instead of glass layer (14). " +
        "Works in combination with AntiWallhack."
    )]
    public bool RaycastAntiWallhack { get; set; } = true;

    [Description("Anti-wallhack for spectators. Spectators only see the players that are currently visible to a living player. (Overwatch still see all players) (There is a delay of approximately 0.3 seconds before players appear.)")]
    public bool SpectatorAntiWallhack { get; set; } = true;

    [Description("Prevents picking up items through walls. Performance impact negligible.")]
    public bool PickupValidation { get; set; } = true;

    [Description("Prevents silent-aim and spinbots. Actually improves performance.")]
    public bool ServersideBacktrack { get; set; } = true;

    [Description("Prevents cheaters from ignoring tesla damage. Performance impact negligible.")]
    public bool ServersideTeslaDamage { get; set; } = true;

    [Description("Prevents cheaters from interacting with doors they're not looking at. Performance impact negligible.")]
    public bool DoorInteractionValidation { get; set; } = true;

    [Description("Detects BigStep movement. Uses DetectAction for server differences greater than 1 unit.")]
    public bool AntiBigStep { get; set; } = true;

    [Description("Detection action: None, Kick, Ban or Kill.")]
    public ActionType DetectAction { get; set; } = ActionType.Ban;

    [Description("Detection ban duration in seconds. Must be positive.")]
    public long DetectBanDuration { get; set; } = 1577000000;

    [Description("Send detection reports to local administrators through the base game report.")]
    public bool DetectReport { get; set; } = true;

    [Description("Discord webhook URL for detection reports (empty to disable).")]
    public string DetectWebhook { get; set; } = "";

    // TODO fix
    /*[Description("Clutters ESP cheats with phantom pickups that disappear when players get close. Performance impact minimal.")]
    public bool PhantomPickups { get; set; } = true;*/

    #region Singleton

    public Config() => Instance = this;
    public static Config Instance { get; private set; } = null!;

    #endregion
}
