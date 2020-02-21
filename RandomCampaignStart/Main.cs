using System.Reflection;
using Harmony;
using HBS.Logging;

// ReSharper disable UnusedMember.Global

namespace RandomCampaignStart
{
    public static class Main
    {
        internal static ILog HBSLog;
        internal static ModSettings Settings;

        // ENTRY POINT
        public static void Init(string modDir, string modSettings)
        {
            var harmony = HarmonyInstance.Create("io.github.mpstark.RandomCampaignStart");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            HBSLog = Logger.GetLogger("RandomCampaignStart");
            Settings = ModSettings.ReadSettings(modSettings);
        }
    }
}
