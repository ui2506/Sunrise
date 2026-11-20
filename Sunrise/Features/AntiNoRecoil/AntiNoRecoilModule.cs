using InventorySystem.Items.Firearms.Modules;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using Sunrise.API.Backtracking;
using Sunrise.API.Detections;

namespace Sunrise.Features.AntiNoRecoil;

internal class AntiNoRecoilModule : PluginModule
{
    const int RequiredIntervals = 8;
    const int EvidenceWindow = 10;
    const float MaxInterval = 0.25f;
    const float MaxSampleGap = 0.09f;
    const float PitchLimitY = 0.9961947f; // sin(85°), leaving 3° before the camera clamps.
    const string PitchLimited = "aim near vertical pitch limit";
    const string NeedMoreSamples = "fewer than two real post-shot samples";

    static readonly Dictionary<Player, Burst> Bursts = [];

    protected override void OnEnabled()
    {
        if (!Config.Instance.AntiNoRecoil)
        {
            Debug.Log("[AntiNoRecoil] Disabled in config.");
            return;
        }

        Handlers.PlayerEvents.ShotWeapon += OnShot;
        Handlers.PlayerEvents.Left += OnLeft;
        Handlers.PlayerEvents.ChangedRole += OnChangedRole;

        base.OnEnabled();
    }

    protected override void OnDisabled()
    {
        Handlers.PlayerEvents.ShotWeapon -= OnShot;
        Handlers.PlayerEvents.Left -= OnLeft;
        Handlers.PlayerEvents.ChangedRole -= OnChangedRole;

        base.OnDisabled();
    }

    protected override void OnReset() => Bursts.Clear();
    static void OnLeft(PlayerLeftEventArgs ev) => Bursts.Remove(ev.Player);
    static void OnChangedRole(PlayerChangedRoleEventArgs ev) => Bursts.Remove(ev.Player);

    static void OnShot(PlayerShotWeaponEventArgs ev)
    {
        if (!BacktrackHistory.Dictionary.TryGetValue(ev.Player.ReferenceHub, out BacktrackHistory history))
        {
            if (Config.Instance.Debug)
                Debug.Log($"[AntiNoRecoil] ShotWeapon {ev.Player.Nickname} ({ev.FirearmItem.Type}): no backtrack history.");

            return;
        }

        if (!Bursts.TryGetValue(ev.Player, out Burst burst))
            Bursts[ev.Player] = burst = new();

        float now = Time.time;
        float previous = burst.LastShot;

        burst.LastShot = now;

        if (burst.Weapon != ev.FirearmItem || now - previous > MaxInterval)
        {
            if (Config.Instance.Debug)
                Debug.Log($"[AntiNoRecoil] ShotWeapon {ev.Player.Nickname} ({ev.FirearmItem.Type}): starting burst. Same weapon: {burst.Weapon == ev.FirearmItem}, previous shot: {now - previous:F3}s ago.");

            burst.Weapon = ev.FirearmItem;
            burst.WindowStart = now;
            burst.Intervals = 0;
            burst.SmoothMask = 0;
            burst.Tolerance = null;

            return;
        }

        if (now < burst.NextReport)
        {
            burst.WindowStart = now;

            if (Config.Instance.Debug)
                Debug.Log($"[AntiNoRecoil] ShotWeapon {ev.Player.Nickname}: report cooldown {burst.NextReport - now:F2}s.");

            return;
        }

        float tolerance = burst.Tolerance ??= GetTolerance(ev.FirearmItem);

        if (tolerance <= 0)
        {
            if (Config.Instance.Debug)
                Debug.Log($"[AntiNoRecoil] ShotWeapon {ev.Player.Nickname} ({ev.FirearmItem.Type}): no eligible recoil pattern or recoil below 0.3 degrees.");

            return;
        }

        float window = now - burst.WindowStart;
        float deviation = 0f, gap = 0f;

        int samples = 0;

        string? rejection = window <= MaxInterval
            ? CheckInterval(history, burst.WindowStart, now, out deviation, out samples, out gap)
            : "sample window expired";

        bool waiting = rejection == NeedMoreSamples && window < MaxInterval;

        if (!waiting)
        {
            burst.WindowStart = now;

            if (rejection == null)
            {
                burst.Intervals -= (burst.SmoothMask >> (EvidenceWindow - 1)) & 1;

                int smooth = deviation <= tolerance ? 1 : 0;

                burst.SmoothMask = ((burst.SmoothMask << 1) | smooth) & ((1 << EvidenceWindow) - 1);
                burst.Intervals += smooth;
            }
            else
            {
                burst.SmoothMask = 0;
                burst.Intervals = 0;
            }
        }

        if (Config.Instance.Debug)
        {
            string result = waiting ? "waiting for aim updates" : rejection ?? (deviation <= tolerance ? "smooth" : "deviation above threshold");

            Debug.Log($"[AntiNoRecoil] ShotWeapon {ev.Player.Nickname} ({ev.FirearmItem.Type}): {result}, smooth {burst.Intervals} in last {EvidenceWindow}, window {window:F3}s, samples {samples}, gap {gap:F3}s, peak {deviation:F3}°, threshold {tolerance:F3}°.");
        }

        if (burst.Intervals < RequiredIntervals)
            return;

        burst.Intervals = 0;
        burst.SmoothMask = 0;
        burst.NextReport = now + 10f;

        if (Config.Instance.Debug)
            Debug.Log($"[AntiNoRecoil] Reporting {ev.Player.Nickname} for Possible NoRecoil.");

        Config.Instance.AntiNoRecoilDetectConfig.Handle(ev.Player, "Possible NoRecoil", $"{ev.FirearmItem.Type}: {RequiredIntervals} low-deviation intervals within the last {EvidenceWindow}. Last peak: {deviation:F3} degrees, threshold: {tolerance:F3} degrees.");
    }

    static float GetTolerance(FirearmItem firearm)
    {
        foreach (ModuleBase module in firearm.Base.Modules)
        {
            if (module is RecoilPatternModule recoil)
            {
                float expected = Mathf.Min(recoil.DisplayHipRecoilDegrees, recoil.DisplayAdsRecoilDegrees);
                return expected >= 0.3f ? Mathf.Min(expected * 0.18f, 0.24f) : 0f;
            }
        }

        return 0f;
    }

    static string? CheckInterval(BacktrackHistory history, float shot, float now, out float peak, out int samples, out float gap)
    {
        peak = 0f;
        samples = 0;
        gap = 0f;
        var entries = history.Entries;
        int i = 0;

        while (i < entries.Size && (entries[i].IsForecast || entries[i].Timestamp > shot))
        {
            i++;
        }

        if (i == entries.Size)
            return "no real sample before shot";

        gap = shot - entries[i].Timestamp;

        if (gap > MaxSampleGap)
            return "stale pre-shot sample";

        BacktrackEntry start = entries[i];

        int older = i + 1;

        while (older < entries.Size && (entries[older].IsForecast || entries[older].Timestamp == start.Timestamp))
            older++;

        if (older == entries.Size)
            return "missing second pre-shot sample";

        float dt = start.Timestamp - entries[older].Timestamp;

        gap = dt;

        if (dt <= 0 || dt > MaxSampleGap)
            return "invalid pre-shot sample spacing";

        Vector3 forward = start.Rotation * Vector3.forward;
        Vector3 previousForward = entries[older].Rotation * Vector3.forward;

        if (Mathf.Abs(forward.y) >= PitchLimitY || Mathf.Abs(previousForward.y) >= PitchLimitY)
            return PitchLimited;

        Vector3 drift = (forward - previousForward) / dt;

        float last = start.Timestamp;

        while (--i >= 0)
        {
            BacktrackEntry sample = entries[i];

            if (sample.IsForecast || sample.Timestamp <= last)
                continue;

            gap = sample.Timestamp - last;

            if (gap > MaxSampleGap)
                return "gap between aim updates";

            Vector3 actual = sample.Rotation * Vector3.forward;

            if (Mathf.Abs(actual.y) >= PitchLimitY)
                return PitchLimited;

            Vector3 predicted = forward + drift * (sample.Timestamp - start.Timestamp);

            samples++;

            peak = Mathf.Max(peak, Vector3.Angle(predicted, actual));
            last = sample.Timestamp;
        }

        gap = now - last;

        if (gap > MaxSampleGap)
            return "stale final sample";

        if (samples < 2)
            return NeedMoreSamples;

        return null;
    }

    class Burst
    {
        public FirearmItem? Weapon;
        public float LastShot;
        public float WindowStart;
        public float NextReport;
        public float? Tolerance;
        public int Intervals;
        public int SmoothMask;
    }
}
