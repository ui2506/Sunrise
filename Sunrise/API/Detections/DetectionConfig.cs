using LabApi.Features.Wrappers;
using System.ComponentModel;

namespace Sunrise.API.Detections
{
    public class DetectionConfig
    {
        [Description("Detection action: None, Kick, Ban or Kill.")]
        public ActionType DetectAction { get; set; } = ActionType.Ban;

        [Description("Detection ban duration in seconds. Must be positive.")]
        public long DetectBanDuration { get; set; } = 1577000000;

        [Description("Send detection reports to local administrators through the base game report.")]
        public bool DetectReport { get; set; } = true;

        [Description("Discord webhook URL for detection reports (empty to disable).")]
        public string DetectWebhook { get; set; } = "";

        internal void Handle(Player player, string detection, string details) => Handle(player, detection, details, DetectAction);

        internal void Handle(Player player, string detection, string details, ActionType action) => DetectionHandler.Handle(player, detection, details, action, this);
    }
}
