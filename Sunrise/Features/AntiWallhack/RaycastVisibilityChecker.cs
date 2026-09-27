using LabApi.Features.Wrappers;
using PlayerRoles.PlayableScps;

namespace Sunrise.Features.AntiWallhack;

/*
With 30 players randomly distributed in 30*30 area (approximate worst case) consumes around 130ms per 10 seconds
[02:24:46] [Sunrise] Time consumed by RaycastVisibilityChecker in last 10.01: 0.15 (1.45%)
[02:24:56] [Sunrise] Time consumed by RaycastVisibilityChecker in last 10.01: 0.13 (1.27%)
[02:25:06] [Sunrise] Time consumed by RaycastVisibilityChecker in last 10.01: 0.13 (1.27%)
[02:25:16] [Sunrise] Time consumed by RaycastVisibilityChecker in last 10.00: 0.12 (1.25%)
 */
internal static class RaycastVisibilityChecker
{
    static readonly Vector2[] VisibilityPointOffsets =
    [
        new(0, 0), // camera

        new(0.65f, -0.13f), // right shoulder (far)
        new(-0.65f, -0.13f), // left shoulder (far)

        new(0.3f, -1.03f), // right knee (far)
        new(-0.3f, -1.03f), // left knee (far)

        new(0f, 0.57f), // above the head

        new(0.27f, -1.43f), // near the feet on the right
        new(-0.27f, -1.43f), // near the feet on the left

        new(0.07f, -0.13f), // right shoulder
        new(-0.07f, -0.13f), // left shoulder

        new(0, -0.63f), // center of the body
    ];

    static readonly Vector3[] VisibilityPointsBuffer = new Vector3[VisibilityPointOffsets.Length + 1];

    static readonly AutoBenchmark Benchmark = new("Raycast Anti Wallhack");

    public static bool IsVisible(Player playerA, Player playerB)
    {
        int key = RaycastVisibilityCache.GetKey(playerA.PlayerId, playerB.PlayerId);

        if (RaycastVisibilityCache.TryGet(key, out bool value))
            return value;

        Benchmark.Start();
        bool result = CheckAnyVisibility(playerA, playerB);
        RaycastVisibilityCache.Save(key, result);
        Benchmark.Increment();
        Benchmark.Stop();
        return result;
    }

    static bool CheckAnyVisibility(Player playerA, Player playerB) => CheckDirectionalVisibility(playerA, playerB) || CheckDirectionalVisibility(playerB, playerA);

    static bool CheckDirectionalVisibility(Player observer, Player target)
    {
        Vector3 observerPosition = observer.Camera.position;
        Vector3 targetPosition = target.Camera.position;

        Vector3 directionToObserver = (observerPosition - targetPosition).normalized;

        SetVisibilityPoints(targetPosition, Vector3.Cross(directionToObserver, Vector3.up), Vector3.up, target);

        foreach (Vector3 point in VisibilityPointsBuffer)
        {
            if (Physics.Linecast(observerPosition, point, VisionInformation.VisionLayerMask))
            {
                Debug.DrawPoint(point, Colors.Orange * 50, 0.11f);
                continue;
            }

            Debug.DrawLine(observerPosition, point, Colors.Blue * 50, 0.11f);
            return true;
        }

        return false;
    }

    static void SetVisibilityPoints(Vector3 position, Vector3 right, Vector3 up, Player player)
    {
        float rightForecastValue = Vector3.Dot(player.Velocity * Config.Instance.AccountedLatencySeconds, right);
        Vector3 rightForecast = rightForecastValue * right; // Project forecast on the 'right' vector

        for (var i = 0; i < VisibilityPointOffsets.Length; i++)
        {
            Vector2 cameraSpaceOffset = VisibilityPointOffsets[i];
            Vector3 worldSpaceOffset = cameraSpaceOffset.x * right + cameraSpaceOffset.y * up;

            if (rightForecastValue != 0 && cameraSpaceOffset.x != 0)
            {
                // Right side && moving right || Left side && moving left
                if ((cameraSpaceOffset.x < 0 && rightForecastValue < 0) || (cameraSpaceOffset.x > 0 && rightForecastValue > 0))
                {
                    VisibilityPointsBuffer[i] = position + worldSpaceOffset + rightForecast;
                    continue;
                }
            }

            VisibilityPointsBuffer[i] = position + worldSpaceOffset;
        }

        VisibilityPointsBuffer[VisibilityPointsBuffer.Length - 1] = player.CurrentItem is FirearmItem ?
            position + player.Camera.rotation * new Vector3(0, -0.5f, 1.2f) :
            position + player.Camera.rotation * new Vector3(0, -0.5f, 0.5f);
    }
}

internal static class RaycastVisibilityCache
{
    const float NotVisibleCacheTime = 0.1f;
    const float VisibleCacheTime = 0.5f;

    static readonly Dictionary<int, (float Expiration, bool Value)> CachedVisibility = new();

    internal static void Clear() => CachedVisibility.Clear();

    internal static void Save(int key, bool value)
    {
        float cacheTime = value ? VisibleCacheTime : NotVisibleCacheTime;
        CachedVisibility[key] = (Time.time + cacheTime, value);
    }

    internal static bool TryGet(int key, out bool value)
    {
        if (CachedVisibility.TryGetValue(key, out (float Expiration, bool Value) cache))
        {
            if (cache.Expiration - Time.time > 0)
            {
                value = cache.Value;
                return true;
            }
        }

        value = false;
        return false;
    }

    /// <summary>
    ///     Creates a unique order independent key for two player IDs.
    ///     Will start having collisions after 65535 players, which is way more than practical numbers.
    /// </summary>
    public static int GetKey(int a, int b)
    {
        if (a > b)
            return (a << 16) | b;
        else
            return (b << 16) | a;
    }
}