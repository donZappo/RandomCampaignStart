using BattleTech;
using Harmony;
using RandomCampaignStart.Features;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace RandomCampaignStart.Patches
{
    [HarmonyPatch(typeof(SimGameState), "FirstTimeInitializeDataFromDefs")]
    public static class SimGameState_FirstTimeInitializeDataFromDefs_Patch
    {
        public static void Postfix(SimGameState __instance)
        {
            RandomizeSimGame.TryRandomize(__instance);
        }
    }
}
