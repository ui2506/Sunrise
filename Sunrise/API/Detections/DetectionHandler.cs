using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Sunrise.API.Detections;

internal class DetectionHandler : PluginModule
{
    static readonly AutoBenchmark HandleBenchmark = new("DetectionHandler.Handle (includes Report)");
    static readonly AutoBenchmark ReportBenchmark = new("DetectionHandler.Report");

    static readonly Dictionary<Player, List<float>> ReportCount = [];
    static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };

    protected override void OnEnabled()
    {
        Handlers.PlayerEvents.Left += OnLeft;

        base.OnEnabled();
    }

    protected override void OnDisabled()
    {
        Handlers.PlayerEvents.Left -= OnLeft;

        base.OnDisabled();
    }

    protected override void OnReset() => ReportCount.Clear();

    static void OnLeft(PlayerLeftEventArgs ev) => ReportCount.Remove(ev.Player);

    internal static bool CanReport(Player player) => !ReportCount.TryGetValue(player, out List<float> reports) || reports.Count == 0 || Time.time >= reports[reports.Count - 1] + 10;

    internal static void Handle(Player player, string detection, string details, ActionType action, DetectionConfig config)
    {
        using var measurement = HandleBenchmark.Start();

        if (!ReportCount.TryGetValue(player, out List<float> reports))
        {
            reports = [];
            ReportCount[player] = reports;
        }

        if (action != ActionType.Ban && Config.Instance.MaxWarnCount > 0)
        {
            reports.Add(Time.time);

            if (reports.Count(time => Time.time - time < Config.Instance.MaxWarnCountWindow) >= Config.Instance.MaxWarnCount)
            {
                action = ActionType.Ban;
                details += $"\nBan triggered by the warning threshold: at least {Config.Instance.MaxWarnCount} warnings in the last {Config.Instance.MaxWarnCountWindow} seconds.";
            }
        }

        string reason = Report(player, detection, details, config);

        switch (action)
        {
            case ActionType.Kick:
                if (!player.Kick(reason))
                    Log.Warn($"[DetectionHandler] Kick was rejected for {player.UserId}.");

                break;

            case ActionType.Ban:
                if (config.DetectBanDuration <= 0)
                {
                    Log.Warn($"[DetectionHandler] DetectBanDuration must be between 1 and {uint.MaxValue} seconds. No punishment applied.");
                    break;
                }

                if (!player.Ban(reason, config.DetectBanDuration))
                    Log.Warn($"[DetectionHandler] Ban was rejected for {player.UserId}.");

                break;

            case ActionType.Kill:
                if (!player.Kill(reason))
                    Log.Warn($"[DetectionHandler] Kill was rejected for {player.UserId}.");

                break;
        }
    }

    internal static string Report(Player player, string detection, string details, DetectionConfig config)
    {
        using var measurement = ReportBenchmark.Start();

        string reason = $"[Sunrise] {detection} detected!";
        string report = $"{reason}\nPlayer [{player.PlayerId}] {player.Nickname} ({player.UserId})\n{details}";

        Log.Warn(report);

        if (CanReport(player))
        {
            if (!string.IsNullOrWhiteSpace(config.DetectWebhook))
                _ = SendWebhook(config.DetectWebhook, report);

            if (config.DetectReport)
                SendReport(player, $"{reason}\n{details}");
        }

        return reason;
    }

    static void SendReport(Player player, string reason)
    {
        if (Server.Host is not Player host)
            return;

        PlayerReportingCheaterEventArgs ev = new(host.ReferenceHub, player.ReferenceHub, reason);
        Handlers.PlayerEvents.OnReportingCheater(ev);

        if (!ev.IsAllowed)
            return;

        host.ReferenceHub.GetComponent<CheaterReport>().SendStaffChatNotification(host.UserId, player.UserId, ev.Reason, "Sunrise Anti-Cheat", player.Nickname);
        Handlers.PlayerEvents.OnReportedCheater(new PlayerReportedCheaterEventArgs(host.ReferenceHub, player.ReferenceHub, ev.Reason));
    }

    static async Task SendWebhook(string webhook, string report)
    {
        if (string.IsNullOrWhiteSpace(webhook))
            return;

        try
        {
            string json = Utf8Json.JsonSerializer.ToJsonString(new
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

public enum ActionType
{
    None,
    Kill,
    Kick,
    Ban
}
