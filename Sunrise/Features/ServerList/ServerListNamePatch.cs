using HarmonyLib;

namespace Sunrise.Features.ServerList;

[HarmonyPatch(typeof(ServerConsole), nameof(ServerConsole.RefreshServerNameSafe))]
internal static class ServerListNamePatch
{
    internal static string Tag = string.Empty;

    static void Postfix(ref string __result) => __result += Tag;
}
