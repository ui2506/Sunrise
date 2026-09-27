using LabApi.Features.Wrappers;
using System;
using System.Linq;
using MapGeneration;

namespace Sunrise.API.Visibility.Generation;

internal static class RoomDirectionHelper
{
    internal static Vector3Int[] GetSearchDirections(Room room, out bool known)
    {
        if (RoomVisibilityConfig.KnownDirectionsRooms.TryGetValue(room.Name, out Vector3Int[]? directions))
        {
            known = true;
            return ApplyRoomRotation(directions, room.Rotation);
        }

        if (RoomVisibilityConfig.KnownVariantDirections.TryGetValue(RoomVisibilityConfig.GetVariant(room), out directions))
        {
            known = true;
            return ApplyRoomRotation(directions, room.Rotation);
        }

        known = false;
        return RoomVisibilityConfig.DefaultDirections;
    }

    static Vector3Int[] ApplyRoomRotation(Vector3Int[] directions, Quaternion rotation)
    {
        return directions.Select(d =>
        {
            Vector3 rotated = rotation * d;

            return new Vector3Int(
                Mathf.RoundToInt(rotated.x),
                Mathf.RoundToInt(rotated.y),
                Mathf.RoundToInt(rotated.z)
            );
        }).ToArray();
    }

    internal static void IncludeDirection(Vector3Int startCoords, Vector3Int direction, bool knownConnected, HashSet<Vector3Int> visibleCoords)
    {
        if (direction.sqrMagnitude != 1)
            throw new ArgumentException("Direction must be normalized");

        Vector3Int currentCoords = startCoords + direction;
        Vector3Int previousCoords = startCoords;
        
        while (RoomIdentifier.RoomsByCoords.TryGetValue(currentCoords, out RoomIdentifier? identifier)
            && Room.Get(identifier) is Room room
            && (RoomConnectionChecker.AreConnected(previousCoords, currentCoords) || knownConnected))
        {
            visibleCoords.Add(room.Base.MainCoords);
            previousCoords = currentCoords;
            currentCoords += direction;
            knownConnected = false; // Only first room is guaranteed when direction is known
        }
    }
}