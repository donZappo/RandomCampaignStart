using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BattleTech;
using BattleTech.UI;
using Harmony;
using Newtonsoft.Json;
using static RandomCampaignStart.Logger;
using static RandomCampaignStart.RandomCampaignStart;

// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace RandomCampaignStart
{
    public static class RandomCampaignStart
    {
        internal static Settings ModSettings;
        internal static readonly Random rng = new Random();

        public static void Init(string modDir, string modSettings)
        {
            var harmony = HarmonyInstance.Create("io.github.mpstark.RandomCampaignStart");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            // read settings
            try
            {
                ModSettings = JsonConvert.DeserializeObject<Settings>(modSettings);
                ModSettings.ModDirectory = modDir;
            }
            catch (Exception)
            {
                ModSettings = new Settings();
            }

            Clear();
            if (!ModSettings.Debug) return;
            LogDebug("[SETTINGS]");
            var settingsFields = typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            foreach (var field in settingsFields)
            {
                if (field.GetValue(ModSettings) is IEnumerable &&
                    !(field.GetValue(ModSettings) is string))
                {
                    LogDebug(field.Name);
                    foreach (var item in (IEnumerable) field.GetValue(ModSettings))
                    {
                        LogDebug("\t" + item);
                    }
                }
                else
                {
                    LogDebug($"{field.Name,-30}: {field.GetValue(ModSettings)}");
                }
            }

            LogDebug("[END SETTINGS]");
        }

        // from https://stackoverflow.com/questions/273313/randomize-a-listt
        internal static void RNGShuffle<T>(this IList<T> list)
        {
            var n = list.Count;
            while (n > 1)
            {
                n--;
                var k = rng.Next(n + 1);
                var value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        internal static List<T> GetRandomSubList<T>(List<T> list, int number)
        {
            var subList = new List<T>();

            if (list.Count <= 0 || number <= 0)
                return subList;

            var randomizeMe = new List<T>(list);

            // add enough duplicates of the list to satisfy the number specified
            while (randomizeMe.Count < number)
                randomizeMe.AddRange(list);

            randomizeMe.RNGShuffle();
            for (var i = 0; i < number; i++)
                subList.Add(randomizeMe[i]);

            return subList;
        }
    }

    [HarmonyPatch(typeof(SimGameState), "FirstTimeInitializeDataFromDefs")]
    public static class SimGameState_FirstTimeInitializeDataFromDefs_Patch
    {
        public static void Postfix(SimGameState __instance)
        {
            var simGame = __instance;
            if (ModSettings.StartingRonin.Count + ModSettings.NumberRandomRonin + ModSettings.NumberProceduralPilots > 0)
            {
                while (__instance.PilotRoster.Count > 0)
                {
                    __instance.PilotRoster.RemoveAt(0);
                }

                List<PilotDef> list = new List<PilotDef>();

                if (ModSettings.StartingRonin != null && ModSettings.NumberRoninFromList > 0)
                {
                    var RoninRandomizer = new List<string>();
                    RoninRandomizer.AddRange(GetRandomSubList(ModSettings.StartingRonin, ModSettings.NumberRoninFromList));
                    foreach (var roninID in RoninRandomizer)
                    {
                        var pilotDef = __instance.DataManager.PilotDefs.Get(roninID);
                        LogDebug("Adding starter ronin " + pilotDef.Description.Callsign);
                        // add directly to roster, don't want to get duplicate ronin from random ronin
                        if (pilotDef != null)
                        {
                            // had to log out values to figure out what these presets were called for the stock ronin
                            var portraitString = string.Join("", new[] {"PortraitPreset_", roninID.Split('_')[3]});
                            pilotDef.PortraitSettings = simGame.DataManager.PortraitSettings.Get(portraitString);
                            __instance.AddPilotToRoster(pilotDef, true, true);
                        }
                    }
                }

                LogDebug($"NumberRandomRonin {ModSettings.NumberRandomRonin}");
                if (ModSettings.NumberRandomRonin > 0)
                {
                    List<PilotDef> list2 = new List<PilotDef>(__instance.RoninPilots);
                    for (int m = list2.Count - 1; m >= 0; m--)
                    {
                        for (int n = 0; n < __instance.PilotRoster.Count; n++)
                        {
                            if (list2[m].Description.Id == __instance.PilotRoster[n].Description.Id)
                            {
                                list2.RemoveAt(m);
                                break;
                            }
                        }
                    }

                    list2.RNGShuffle<PilotDef>();
                    for (int i = 0; i < ModSettings.NumberRandomRonin; i++)
                    {
                        list.Add(list2[i]);
                    }
                }

                LogDebug($"NumberProceduralPilots {ModSettings.NumberProceduralPilots}");
                if (ModSettings.NumberProceduralPilots > 0)
                {
                    List<PilotDef> list3;
                    List<PilotDef> collection = __instance.PilotGenerator.GeneratePilots(ModSettings.NumberProceduralPilots, 1, 0f, out list3);
                    list.AddRange(collection);
                }

                foreach (PilotDef def in list)
                {
                    __instance.AddPilotToRoster(def, true, true);
                }
            }

            LogDebug($"[START LANCE CREATION {ModSettings.MinimumStartingWeight}-{ModSettings.MaximumStartingWeight} TONS]");
            var AncestralMechDef = new MechDef(__instance.DataManager.MechDefs.Get(__instance.ActiveMechs[0].Description.Id), __instance.GenerateSimGameUID());
            FullRandom(__instance, AncestralMechDef);
        }

        private static void FullRandom(SimGameState __instance, MechDef AncestralMechDef)
        {
            var originalLance = __instance.ActiveMechs;
            var lance = new List<MechDef>();
            var lanceWeight = 0;
            var mechDefs = __instance.DataManager.MechDefs.Select(kvp => kvp.Value).ToList();
            mechDefs.Shuffle();
            if (ModSettings.RemoveAncestralMech)
            {
                __instance.ActiveMechs.Remove(0);
                if (AncestralMechDef.Description.Id == "mechdef_centurion_TARGETDUMMY" &&
                    ModSettings.IgnoreAncestralMech)
                {
                    ModSettings.MaximumLanceSize = ModSettings.MaximumLanceSize == 6
                        ? 6
                        : ModSettings.MaximumLanceSize + 1;
                    ModSettings.MinimumLanceSize = ModSettings.MinimumLanceSize == 1
                        ? 1
                        : ModSettings.MinimumLanceSize - 1;
                }
            }

            else if (ModSettings.IgnoreAncestralMech)
            {
                lance.Add(AncestralMechDef);
                ModSettings.MaximumLanceSize = ModSettings.MaximumLanceSize == 6
                    ? 6
                    : ModSettings.MaximumLanceSize + 1;
            }

            else
            {
                lance.Add(AncestralMechDef);
                lanceWeight += (int) AncestralMechDef.Chassis.Tonnage;
            }

            while (lance.Count < ModSettings.MinimumLanceSize ||
                   lanceWeight < ModSettings.MinimumStartingWeight)
            {
                var matchingMechs = mechDefs
                    .Except(lance)
                    .Where(mech => mech.Chassis.Tonnage <= ModSettings.MaximumMechWeight &&
                                   mech.Chassis.Tonnage + lanceWeight <= ModSettings.MaximumStartingWeight)
                    .Where(mech => !mech.MechTags.Contains("BLACKLISTED"))
                    .Where(mech => !ModSettings.ExcludedMechs.Contains(mech.Chassis.VariantName));

                if (ModSettings.MechsAdhereToTimeline)
                {
                    matchingMechs =
                        matchingMechs.Where(mech => mech.MinAppearanceDate <=
                                                    UnityGameInstance.BattleTechGame.Simulation.CampaignStartDate);
                }

                if (!ModSettings.AllowDuplicateChassis)
                {
                    matchingMechs = matchingMechs.Distinct(new MechDefComparer());
                }

                if (matchingMechs.Count() < ModSettings.MinimumLanceSize - lance.Count ||
                    matchingMechs.Select(x => x.Chassis.Tonnage).Sum() < ModSettings.MinimumStartingWeight)
                {
                    LogDebug("[INSUFFICIENT MECHS - DEFAULT LANCE CREATION]");
                    __instance.ActiveMechs = originalLance;
                    return;
                }

                var mechDefString = matchingMechs
                    .ElementAt(UnityEngine.Random.Range(0, matchingMechs.Count())).Description.Id
                    .Replace("chassisdef", "mechdef");

                var mechDef = new MechDef(
                    __instance.DataManager.MechDefs.Get(mechDefString), __instance.GenerateSimGameUID());
                LogDebug("Generated " + mechDefString);

                if (mechDef.Chassis.Tonnage + lanceWeight <= ModSettings.MaximumStartingWeight &&
                    lance.Count < ModSettings.MaximumLanceSize)
                {
                    lance.Add(mechDef);
                    lanceWeight += (int) mechDef.Chassis.Tonnage;
                    LogDebug($"Adding {mechDef.Description.Id}, up to {lanceWeight}T");
                }

                LogDebug($"[TONS {lanceWeight} (CLAMP: {ModSettings.MinimumStartingWeight}-{ModSettings.MaximumStartingWeight}), MECHS {lance.Count} (CLAMP: {ModSettings.MinimumLanceSize}-{ModSettings.MaximumLanceSize})");

                if (lance.Count == ModSettings.MaximumLanceSize &&
                    lanceWeight < ModSettings.MinimumStartingWeight ||
                    lance.Count < ModSettings.MinimumLanceSize &&
                    lanceWeight >= ModSettings.MinimumStartingWeight)
                {
                    LogDebug("[BAD LANCE]");
                    lance.Clear();
                    lanceWeight = 0;
                }
            }

            LogDebug("[COMPLETE: ADDING MECHS]");

            var sb = new StringBuilder();
            for (var x = 0;
                x < lance.Count;
                x++)
            {
                sb.AppendLine($"{lance[x].Chassis.VariantName} {lance[x].Name} ({((DateTime) lance[x].MinAppearanceDate).Year}) {lance[x].Chassis.Tonnage}T ({lance[x].Chassis.weightClass})");
                LogDebug($"Mech {x + 1} is {lance[x].Name,-15} {lance[x].Chassis.VariantName,-10} {((DateTime) lance[x].MinAppearanceDate).Year,5} ({lance[x].Chassis.weightClass} {lance[x].Chassis.Tonnage}T)");
                __instance.AddMech(x, lance[x], true, true, false);
            }

            var tonnage = lance.GroupBy(x => x).Select(mech => mech.Key.Chassis.Tonnage).Sum();
            LogDebug($"Tonnage: {tonnage}");
            if (ModSettings.Reroll)
            {
                GenericPopupBuilder
                    .Create("This is your starting lance (" + tonnage + "T)", sb.ToString())
                    .AddButton("Proceed")
                    .AddButton("Re-roll", delegate { FullRandom(__instance, AncestralMechDef); })
                    .CancelOnEscape()
                    .Render();
            }

            else
            {
                GenericPopupBuilder
                    .Create("This is your starting lance " + tonnage + "T", sb.ToString())
                    .AddButton("Proceed")
                    .CancelOnEscape()
                    .Render();
            }
        }
    }

    internal class Settings
    {
        public List<string> StartingRonin = new List<string>();
        public List<string> ExcludedMechs = new List<string>();
        public float MinimumStartingWeight = 165;
        public float MaximumStartingWeight = 175;
        public float MaximumMechWeight = 50;
        public int MinimumLanceSize = 4;
        public int MaximumLanceSize = 6;
        public bool AllowDuplicateChassis = false;

        public int NumberRoninFromList = 4;

        public int NumberProceduralPilots = 0;
        public int NumberRandomRonin = 4;

        public bool RemoveAncestralMech = false;
        public bool IgnoreAncestralMech = true;

        public string ModDirectory = string.Empty;
        public bool Debug = false;

        public bool MechsAdhereToTimeline = true;
        public int StartYear = -1;
        public bool Reroll = true;
    }
}