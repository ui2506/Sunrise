using LabApi.Features.Wrappers;
using LiteNetLib;
using Mirror.LiteNetLib4Mirror;

namespace Sunrise.Utility;

internal static class InteractionRaycast
{
    internal static double GetLatency(Player player) =>
        LiteNetLib4MirrorServer.Peers.TryGetValue(player.ConnectionToClient.connectionId, out NetPeer peer)
            ? peer.Ping * 0.001 * player.ReferenceHub.searchCoordinator.serverDelayThreshold
            : 0d;

    internal static bool TryGetNearbyHit(Player player, Ray ray, float distance, int mask, out RaycastHit hit)
    {
        hit = default;

        float pingRadius = Mathf.Clamp((float)GetLatency(player), 0f, 0.12f);
        float tpsRadius = 0.12f * Mathf.Clamp01(1f - (float)Server.Tps / 60f);
        float radius = (pingRadius + tpsRadius) * 1.5f;

        if (radius <= 0f || !Physics.SphereCast(ray, radius, out RaycastHit nearby, distance, mask))
            return false;

        Vector3 direction = nearby.point - ray.origin;

        if (nearby.distance <= 0f || direction.sqrMagnitude > distance * distance)
            return false;

        return Physics.Raycast(ray.origin, direction, out hit, distance, mask) && hit.collider == nearby.collider;
    }
}
