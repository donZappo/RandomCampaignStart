using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BattleTech;
using BattleTech.UI;
using Harmony;
using HBS;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto.Parameters;
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
                // LogDebug("Randomizing pilots, removing old pilots");
                //
                // // clear roster
                // while (simGame.PilotRoster.Count > 0)
                //     simGame.PilotRoster.RemoveAt(0);
                //
                // // starting ronin that are always present
                // if (ModSettings.StartingRonin.Count > 0)
                // {
                //     for (var i = 0; i < ModSettings.NumberRoninFromList; i++)
                //     {
                //         var pilotID = simGame.Constants.Story.StartingMechWarriors[UnityEngine.Random.Range(0, 3)];
                //
                //         //foreach (var pilotID in ModSettings.StartingRonin)
                //         //{
                //         if (!simGame.DataManager.PilotDefs.Exists(pilotID))
                //         {
                //             LogDebug($"\tMISSING StartingRonin {pilotID}!");
                //             continue;
                //         }
                //
                //         var pilotDef = simGame.DataManager.PilotDefs.Get(pilotID);
                //         try
                //         {
                //             // had to log out values to figure out what these presets were called for the stock ronin
                //             var portraitString = string.Join("", new[] {"PortraitPreset_", pilotID.Split('_')[3]});
                //             pilotDef.PortraitSettings = simGame.DataManager.PortraitSettings.Get(portraitString);
                //         }
                //         catch (Exception ex)
                //         {
                //             Error(ex);
                //         }
                //
                //         simGame.AddPilotToRoster(pilotDef, true);
                //         LogDebug($"\tAdding StartingRonin {pilotDef.Description.Id}");
                //     }
                // }
                //
                // // random ronin
                // if (ModSettings.NumberRandomRonin > 0)
                // {
                //     // make sure to remove the starting ronin list from the possible random pilots! yay linq
                //     var randomRonin = GetRandomSubList(
                //         simGame.RoninPilots.Where(x => !ModSettings.StartingRonin.Contains(x.Description.Id)).ToList(),
                //         ModSettings.NumberRandomRonin);
                //     foreach (var pilotDef in randomRonin)
                //     {
                //         simGame.AddPilotToRoster(pilotDef, true);
                //         LogDebug($"\tAdding random Ronin {pilotDef.Description.Id}");
                //     }
                // }
                //
                // // random procedural pilots
                // if (ModSettings.NumberProceduralPilots > 0)
                // {
                //     LogDebug($"NumberProceduralPilots {ModSettings.NumberProceduralPilots}");
                //     var randomProcedural = simGame.PilotGenerator.GeneratePilots(ModSettings.NumberProceduralPilots, 0, 0, out _);
                //     foreach (var pilotDef in randomProcedural)
                //     {
                //         simGame.AddPilotToRoster(pilotDef, true);
                //         LogDebug($"\tAdding random procedural pilot {pilotDef.Description.Id}");
                //     }
                // }

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

            LogDebug($"Starting lance creation {ModSettings.MinimumStartingWeight} - {ModSettings.MaximumStartingWeight} tons");
            // mechs
            if (!ModSettings.UseRandomMechs) return;
            var AncestralMechDef = new MechDef(__instance.DataManager.MechDefs.Get(__instance.ActiveMechs[0].Description.Id), __instance.GenerateSimGameUID());
            var RemoveAncestralMech = ModSettings.RemoveAncestralMech || AncestralMechDef.Description.Id == "mechdef_centurion_TARGETDUMMY";

            var lance = new List<MechDef>();
            var baySlot = 1;

            // clear the initial lance
            __instance.ActiveMechs.Clear();

            var chassisList = new List<ChassisDef>();
            foreach (var kvp in __instance.DataManager.ChassisDefs)
            {
                // not sure if this is where these strings actually appear
                if (kvp.Value.Description.Id.Contains("DUMMY") &&
                    !kvp.Value.Description.Id.Contains("CUSTOM"))
                    //if (kvp.Key.Contains("DUMMY") && !kvp.Key.Contains("CUSTOM"))
                {
                    // just in case someone calls their mech DUMMY
                    continue;
                }

                if (kvp.Value.Description.Id.Contains("CUSTOM") ||
                    kvp.Value.Description.Id.Contains("DUMMY"))
                    //if (kvp.Key.Contains("CUSTOM") || kvp.Key.Contains("DUMMY"))
                {
                    continue;
                }

                if (ModSettings.MaximumMechWeight != 100)
                {
                    if (kvp.Value.Tonnage > ModSettings.MaximumMechWeight ||
                        kvp.Value.Tonnage < 20)
                    {
                        continue;
                    }
                }

                // passed checks, add to List
                chassisList.Add(kvp.Value);
            }

            bool firstrun = true;
            for (int xloop = 0;
                xloop < ModSettings.Loops;
                xloop++)
            {
                var LanceCounter = 1;
                float currentLanceWeight;
                if (!ModSettings.FullRandomMode)
                {
                    // remove ancestral mech if specified
                    if (RemoveAncestralMech && firstrun)
                    {
                        __instance.ActiveMechs.Remove(0);
                    }

                    currentLanceWeight = 0;

                    while (currentLanceWeight < ModSettings.MinimumStartingWeight || currentLanceWeight > ModSettings.MaximumStartingWeight)
                    {
                        if (RemoveAncestralMech)
                        {
                            currentLanceWeight = 0;
                            baySlot = 0;
                        }
                        else
                        {
                            currentLanceWeight = AncestralMechDef.Chassis.Tonnage;
                            baySlot = 1;
                        }

                        if (!firstrun)
                        {
                            __instance.ActiveMechs.Clear();
                        }

                        //It's not a BUG, it's a FEATURE.
                        LanceCounter++;
                        if (LanceCounter > ModSettings.SpiderLoops)
                        {
                            var mechDefSpider = new MechDef(__instance.DataManager.MechDefs.Get("mechdef_spider_SDR-5V"), __instance.GenerateSimGameUID());
                            lance.Add(mechDefSpider); // worry about sorting later
                            for (int j = baySlot; j < 6; j++)
                            {
                                __instance.AddMech(j, mechDefSpider, true, true, false);
                            }

                            break;
                        }

                        var legacyLance = new List<string>();
                        legacyLance.AddRange(GetRandomSubList(ModSettings.AssaultMechsPossible, ModSettings.NumberAssaultMechs));
                        legacyLance.AddRange(GetRandomSubList(ModSettings.HeavyMechsPossible, ModSettings.NumberHeavyMechs));
                        legacyLance.AddRange(GetRandomSubList(ModSettings.MediumMechsPossible, ModSettings.NumberMediumMechs));
                        legacyLance.AddRange(GetRandomSubList(ModSettings.LightMechsPossible, ModSettings.NumberLightMechs));

                        // check to see if we're on the last mechbay and if we have more mechs to add
                        // if so, store the mech at index 5 before next iteration.
                        for (var j = 0; j < legacyLance.Count; j++)
                        {
                            LogDebug("Build Lance");

                            var mechDef2 = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[j]), __instance.GenerateSimGameUID());
                            __instance.AddMech(baySlot, mechDef2, true, true, false);
                            if (baySlot == 5 && j + 1 < legacyLance.Count)
                            {
                                __instance.UnreadyMech(5, mechDef2);
                            }
                            else
                            {
                                baySlot++;
                            }

                            currentLanceWeight += (int) mechDef2.Chassis.Tonnage;
                        }

                        firstrun = false;
                        if (currentLanceWeight >= ModSettings.MinimumStartingWeight && currentLanceWeight <= ModSettings.MaximumStartingWeight)
                        {
                            LogDebug("Classic Mode");
                            for (var y = 0; y < __instance.ActiveMechs.Count(); y++)
                            {
                                LogDebug($"{__instance.ActiveMechs[y].Description.Id}");
                            }
                        }
                        else
                        {
                            LogDebug("Illegal Lance");
                        }
                    }
                }
                else
                {
                    FullRandom(__instance, lance, AncestralMechDef);
                }
            }
        }

        private static void FullRandom(SimGameState __instance, List<MechDef> lance, MechDef AncestralMechDef)
        {
            LogDebug("Full random mode");
            lance.Clear();
            var lanceWeight = 0;

            var mechDefs = __instance.DataManager.MechDefs.Select(kvp => kvp.Value).ToList();
            if (ModSettings.RemoveAncestralMech)
            {
                __instance.ActiveMechs.Remove(0);
                if (AncestralMechDef.Description.Id == "mechdef_centurion_TARGETDUMMY" &&
                    ModSettings.IgnoreAncestralMech)
                {
                    ModSettings.MaximumLanceSize++;
                    ModSettings.MinimumLanceSize++;
                }
            }

            else if (ModSettings.IgnoreAncestralMech)
            {
                lance.Add(AncestralMechDef);
                ModSettings.MaximumLanceSize++;
            }

            else
            {
                lance.Add(AncestralMechDef);
                lanceWeight = (int) AncestralMechDef.Chassis.Tonnage;
            }

            while (lance.Count < ModSettings.MinimumLanceSize ||
                   lanceWeight < ModSettings.MinimumStartingWeight)
            {
                var matchingMechs = mechDefs
                    .Where(mech => mech.Chassis.Tonnage <= ModSettings.MaximumMechWeight &&
                                   mech.Chassis.Tonnage + lanceWeight <= ModSettings.MaximumStartingWeight)
                    .Except(lance);
                if (ModSettings.MechsAdhereToTimeline)
                {
                    matchingMechs =
                        matchingMechs.Where(mech => mech.MinAppearanceDate <=
                                                    UnityGameInstance.BattleTechGame.Simulation.CampaignStartDate);
                }

                var mechDefString = matchingMechs.ElementAt(
                        UnityEngine.Random.Range(0, matchingMechs.Count()))
                    .Description.Id.Replace("chassisdef", "mechdef");

                var mechDef = new MechDef(
                    __instance.DataManager.MechDefs.Get(mechDefString), __instance.GenerateSimGameUID());
                LogDebug("Generated " + mechDefString);

                if (mechDef.MechTags.Contains("BLACKLISTED"))
                {
                    LogDebug($"[BLACKLISTED] {mechDef.Name}");
                    continue;
                }

                var flag = false;
                foreach (var mechID in ModSettings.ExcludedMechs)
                {
                    if (mechID != mechDef.Description.Id) continue;
                    LogDebug($"[EXCLUDED] {mechDef.Name}");
                    flag = true;
                }

                if (flag) continue;

                if (!ModSettings.AllowDuplicateChassis)
                {
                    flag = false;
                    foreach (var mech in lance)
                    {
                        if (mech.Name != mechDef.Name) continue;
                        LogDebug($"[DUPE] {mechDef.Name}");
                        flag = true;
                    }

                    if (flag && matchingMechs.Count() == 1)
                    {
                        LogDebug("[BAD LANCE]");
                        lance.Clear();
                        lanceWeight = 0;
                        continue;
                    }

                    if (flag) continue;
                }

                if (mechDef.Chassis.Tonnage + lanceWeight <= ModSettings.MaximumStartingWeight &&
                    lance.Count < ModSettings.MaximumLanceSize)
                {
                    lance.Add(mechDef);
                    lanceWeight += (int) mechDef.Chassis.Tonnage;
                    LogDebug($"Adding {mechDef.Description.Id}, up to {lanceWeight}T");
                }

                LogDebug($"Tonnage {lanceWeight} (clamp: {ModSettings.MinimumStartingWeight}-{ModSettings.MaximumStartingWeight}), mechs {lance.Count} (clamp: {ModSettings.MinimumLanceSize}-{ModSettings.MaximumLanceSize})");

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
                    .AddButton("Re-roll", delegate { FullRandom(__instance, lance, AncestralMechDef); })
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
        public List<string> AssaultMechsPossible = new List<string>();
        public List<string> HeavyMechsPossible = new List<string>();
        public List<string> LightMechsPossible = new List<string>();
        public List<string> MediumMechsPossible = new List<string>();

        public int NumberAssaultMechs = 0;
        public int NumberHeavyMechs = 0;
        public int NumberLightMechs = 3;
        public int NumberMediumMechs = 1;

        public float MinimumStartingWeight = 165;
        public float MaximumStartingWeight = 175;
        public float MaximumMechWeight = 50;
        public int MinimumLanceSize = 4;
        public int MaximumLanceSize = 6;
        public bool AllowCustomMechs = false;
        public bool FullRandomMode = true;
        public bool AllowDuplicateChassis = false;
        public float MechPercentageStartingCost = 0.2f;

        public List<string> StartingRonin = new List<string>();
        public int NumberRoninFromList = 4;
        public List<string> ExcludedMechs = new List<string>();

        public int NumberProceduralPilots = 0;
        public int NumberRandomRonin = 4;

        public bool RemoveAncestralMech = false;
        public bool IgnoreAncestralMech = true;

        public string ModDirectory = string.Empty;
        public bool Debug = false;
        public int SpiderLoops = 1000;
        public int Loops = 1;

        public bool UseRandomMechs = true;
        public bool MechsAdhereToTimeline = true;
        public int StartYear = -1;
        public bool Reroll = true;
    }
}