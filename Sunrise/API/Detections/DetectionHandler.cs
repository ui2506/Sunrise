using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Sunrise.API.Detections;

internal class DetectionHandler : PluginModule
{
    static readonly Dictionary<Player, float> ReportCoolDown = [];
    static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };

    protected override void OnEnabled() => Handlers.PlayerEvents.Left += OnLeft;
    protected override void OnDisabled() => Handlers.PlayerEvents.Left -= OnLeft;
    protected override void OnReset() => ReportCoolDown.Clear();

    static void OnLeft(PlayerLeftEventArgs ev) => ReportCoolDown.Remove(ev.Player);

    internal static void Handle(Player player, string detection, string details)
    {
        if (ReportCoolDown.TryGetValue(player, out float nextReport) && Time.time < nextReport)
            return;

        ReportCoolDown[player] = Time.time + 10f;

        string reason = $"[Sunrise] {detection} detected!";
        string report = $"{reason}\nPlayer [{player.PlayerId}] {player.Nickname} ({player.UserId})\n{details}";

        Log.Warn(report);

        if (!string.IsNullOrWhiteSpace(Config.Instance.DetectWebhook))
            _ = SendWebhook(Config.Instance.DetectWebhook, report);

        if (Config.Instance.DetectReport)
            CheaterReport.SubmitReport("SERVER", player.UserId, $"{reason}\n{details}", player.NetworkId, "Sunrise", player.Nickname, false);

        switch (Config.Instance.DetectAction?.Trim().ToLowerInvariant())
        {
            case "none":
                break;

            case "kick":
                if (!player.Kick(reason))
                    Log.Warn($"[DetectionHandler] Kick was rejected for {player.UserId}.");

                break;

            case "ban":
                if (Config.Instance.DetectBanDuration <= 0)
                {
                    Log.Warn($"[DetectionHandler] DetectBanDuration must be between 1 and {uint.MaxValue} seconds. No punishment applied.");
                    break;
                }

                if (!player.Ban(reason, Config.Instance.DetectBanDuration))
                    Log.Warn($"[DetectionHandler] Ban was rejected for {player.UserId}.");

                break;

            case "kill":
                if (!player.Kill(reason))
                    Log.Warn($"[DetectionHandler] Kill was rejected for {player.UserId}.");

                break;

            default:
                Log.Warn($"[DetectionHandler] Unknown DetectAction '{Config.Instance.DetectAction}'. No punishment applied.");
                break;
        }
    }

    static async Task SendWebhook(string webhook, string report)
    {
        if (string.IsNullOrWhiteSpace(webhook))
            return;

        try
        {
            string json = new JavaScriptSerializer().Serialize(new
            {
                content = report.Length > 2000 ? report.Substring(0, 2000) : report,
                allowed_mentions = new { parse = Array.Empty<string>() },
            });

            using StringContent content = new(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await Client.PostAsync(webhook, content).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                Log.Warn($"[DetectionHandler] Webhook returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception exception)
        {
            Log.Warn($"[DetectionHandler] Webhook failed ({exception.GetType().Name}).");
        }
    }
}
