using LabApi.Features.Wrappers;
using MapGeneration;
using MapGeneration.Holidays;

namespace Sunrise.API.Visibility.Generation;

internal static class RoomVisibilityConfig
{
    public static readonly Dictionary<RoomName, Vector3Int[]> KnownDirectionsRooms = new()
    {
        [RoomName.HczWarhead] = [Vector3Int.forward, Vector3Int.back, Vector3Int.left],
        [RoomName.HczAcroamaticAbatement] = [Vector3Int.forward, Vector3Int.back, Vector3Int.left, Vector3Int.right],
        [RoomName.HczArmory] = [Vector3Int.forward, Vector3Int.back, Vector3Int.left],

        [RoomName.Hcz079] = [Vector3Int.left],
        [RoomName.HczMicroHID] = [Vector3Int.left, Vector3Int.right],
        [RoomName.Hcz939] = [Vector3Int.right, Vector3Int.back],

        [RoomName.EzOfficeSmall] = [Vector3Int.forward, Vector3Int.back],
        [RoomName.EzIntercom] = [Vector3Int.left, Vector3Int.back],
    };

    public static readonly HashSet<RoomName> DiagonalVisibilityRooms =
    [
        RoomName.HczWarhead,
        RoomName.HczAcroamaticAbatement,
        RoomName.HczArmory,
    ];

    // These variants share RoomName.Unnamed in LabApi.
    public static readonly Dictionary<string, Vector3Int[]> KnownVariantDirections = new()
    {
        ["HCZ_Corner_Deep"] = [Vector3Int.back, Vector3Int.right],
        ["HCZ_Intersection_Junk"] = [Vector3Int.forward, Vector3Int.back, Vector3Int.left],
        ["HCZ_Intersection"] = [Vector3Int.forward, Vector3Int.back, Vector3Int.left],
    };

    internal static string GetVariant(Room room)
    {
        string name = room.GameObject.name.Split('(')[0].TrimEnd();
        return HolidayUtils.IsAnyHolidayActive() ? name.Replace(HolidayUtils.GetActiveHoliday().ToString(), "").TrimEnd() : name;
    }

    public static readonly Vector3Int[] DefaultDirections =
    [
        Vector3Int.forward,
        Vector3Int.right,
        Vector3Int.back,
        Vector3Int.left,
    ];
}
