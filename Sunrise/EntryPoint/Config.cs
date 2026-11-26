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

    [Description("Checks hidden pickups about every 0.1 seconds and rechecks visible pickups after 1 second. In crowded areas, extra raycasts continue on the next update. " +
        "Pickups within 3 units of the observer or a directly visible pickup are shown within the normal visibility range. " +
        "Radios, active SCP-244 instances and thrown projectiles are excluded."
    )]
    public bool PickupAntiWallhack { get; set; } = true;

    [Description("If true, hide pickups only until the player first sees them. If false, check visible pickups again and hide them when they leave view.")]
    public bool PickupAntiWallhackKeepSeenPickupsVisible { get; set; } = false;

    [Description("Checks line of sight when a player starts picking up an item and detects pickups that finish too quickly.")]
    public bool PickupValidation { get; set; } = true;
    public DetectionConfig FastPickupDetectConfig { get; set; } = new DetectionConfig
    {
        DetectAction = ActionType.None,
        DetectBanDuration = 1577000000,
        DetectReport = false,
        DetectWebhook = ""
    };

    [Description("Prevents silent-aim and spinbots. Actually improves performance.")]
    public bool ServersideBacktrack { get; set; } = true;

    [Description("Prevents cheaters from ignoring tesla damage. Performance impact negligible.")]
    public bool ServersideTeslaDamage { get; set; } = true;

    [Description("Checks that players are looking at the door, locker compartment, or elevator panel they interact with.")]
    public bool DoorInteractionValidation { get; set; } = true;

    [Description("Detects BigStep movement and corrects suspicious upward movement. Confirmed detections use the configured detection action.")]
    public bool AntiBigStep { get; set; } = true;

    public DetectionConfig AntiBigStepDetectConfig { get; set; } = new DetectionConfig
    {
        DetectAction = ActionType.Ban,
        DetectBanDuration = 1577000000,
        DetectReport = true,
        DetectWebhook = ""
    };

    [Description("Detects sustained bursts with unusually little recoil after accounting for aim movement. Detections use the configured NoRecoil action.")]
    public bool AntiNoRecoil { get; set; } = true;
    public DetectionConfig AntiNoRecoilDetectConfig { get; set; } = new DetectionConfig
    {
        DetectAction = ActionType.Ban,
        DetectBanDuration = 1577000000,
        DetectReport = true,
        DetectWebhook = ""
    };

    [Description("Ban a player when their warnings across all detections reach this threshold within the warning window. " +
        "Detections with an initial action of None, Kick, or Kill count as warnings. Set to 0 to disable warning-based bans.")]
    public int MaxWarnCount { get; set; } = 3;

    [Description("How far back to count a player's warnings, in seconds.")]
    public int MaxWarnCountWindow { get; set; } = 20;

    #region Singleton

    public Config() => Instance = this;
    public static Config Instance { get; private set; } = null!;

    #endregion
}
