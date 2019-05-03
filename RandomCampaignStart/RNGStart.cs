using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BattleTech;
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
    [HarmonyPatch(typeof(MechDef), "CopyFrom")]
    public class CopyPatch
    {
        public static void Prefix(MechDef __instance, MechDef def)
        {
            var mechDef = __instance as ActorDef;
            Traverse.Create(mechDef).Property("MinAppearanceDate").SetValue(def.MinAppearanceDate);
        }
    }

    [HarmonyPatch(typeof(SimGameState), "_OnDefsLoadComplete")]
    public static class Initialize_New_Game
    {
        public static void Postfix(SimGameState __instance)
        {
            float cost = 0;
            foreach (var mechDef in __instance.ActiveMechs.Values)
            {
                cost += mechDef.Description.Cost * RandomCampaignStart.Settings.MechPercentageStartingCost / 100;
            }

            __instance.AddFunds(-(int) cost, null, false);
        }
    }

    public static class RandomCampaignStart
    {
        internal static ModSettings Settings;

        public static void Init(string modDir, string modSettings)
        {
            var harmony = HarmonyInstance.Create("io.github.mpstark.RandomCampaignStart");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            // read settings
            try
            {
                Settings = JsonConvert.DeserializeObject<ModSettings>(modSettings);
                Settings.ModDirectory = modDir;
            }
            catch (Exception)
            {
                Settings = new ModSettings();
            }

            Clear();
        }

        // from https://stackoverflow.com/questions/273313/randomize-a-listt
        internal static readonly Random rng = new Random();

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
            if (RandomCampaignStart.Settings.NumberRandomRonin +
                RandomCampaignStart.Settings.NumberProceduralPilots +
                RandomCampaignStart.Settings.NumberRoninFromList > 0)
            {
                while (__instance.PilotRoster.Count > 0)
                {
                    __instance.PilotRoster.RemoveAt(0);
                }

                List<PilotDef> list = new List<PilotDef>();

                if (RandomCampaignStart.Settings.StartingRonin != null)
                {
                    var RoninRandomizer = new List<string>();
                    RoninRandomizer.AddRange(GetRandomSubList(RandomCampaignStart.Settings.StartingRonin, RandomCampaignStart.Settings.NumberRoninFromList));
                    foreach (var roninID in RoninRandomizer)
                    {
                        var pilotDef = __instance.DataManager.PilotDefs.Get(roninID);

                        // add directly to roster, don't want to get duplicate ronin from random ronin
                        if (pilotDef != null)
                            __instance.AddPilotToRoster(pilotDef, true, true);
                    }
                }

                if (RandomCampaignStart.Settings.NumberRandomRonin > 0)
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

                    list2.RNGShuffle();
                    for (int i = 0; i < RandomCampaignStart.Settings.NumberRandomRonin; i++)
                    {
                        list.Add(list2[i]);
                    }
                }

                if (RandomCampaignStart.Settings.NumberProceduralPilots > 0)
                {
                    List<PilotDef> list3;
                    List<PilotDef> collection = __instance.PilotGenerator.GeneratePilots(RandomCampaignStart.Settings.NumberProceduralPilots, 1, 0f, out list3);
                    list.AddRange(collection);
                }

                foreach (var pilotDef in list)
                {
                    __instance.AddPilotToRoster(pilotDef, true, true);
                }
            }

            //Logger.LogDebug($"Starting lance creation {RngStart.Settings.MinimumStartingWeight} - {RngStart.Settings.MaximumStartingWeight} tons");
            // mechs
            if (RandomCampaignStart.Settings.UseRandomMechs)
            {
                var AncestralMechDef = new MechDef(__instance.DataManager.MechDefs.Get(__instance.ActiveMechs[0].Description.Id), __instance.GenerateSimGameUID());
                bool RemoveAncestralMech = RandomCampaignStart.Settings.RemoveAncestralMech;
                if (AncestralMechDef.Description.Id == "mechdef_centurion_TARGETDUMMY")
                {
                    RemoveAncestralMech = true;
                }

                var lance = new List<MechDef>();
                float currentLanceWeight = 0;
                var baySlot = 1;

                // clear the initial lance
                for (var i = 1; i < 6; i++)
                {
                    __instance.ActiveMechs.Remove(i);
                }

                // memoize dictionary of tonnages since we may be looping a lot
                //Logger.LogDebug($"Memoizing");
                var mechTonnages = new Dictionary<string, float>();
                foreach (var kvp in __instance.DataManager.ChassisDefs)
                {
                    if (kvp.Key.Contains("DUMMY") && !kvp.Key.Contains("CUSTOM"))
                    {
                        // just in case someone calls their mech DUMMY
                        continue;
                    }

                    if (kvp.Key.Contains("CUSTOM") || kvp.Key.Contains("DUMMY"))
                    {
                        continue;
                    }

                    if (RandomCampaignStart.Settings.MaximumMechWeight != 100)
                    {
                        if (kvp.Value.Tonnage > RandomCampaignStart.Settings.MaximumMechWeight || kvp.Value.Tonnage < 20)
                        {
                            continue;
                        }
                    }

                    // passed checks, add to Dictionary
                    mechTonnages.Add(kvp.Key, kvp.Value.Tonnage);
                }

                bool firstrun = true;
                for (int xloop = 0; xloop < RandomCampaignStart.Settings.Loops; xloop++)
                {
                    int LanceCounter = 1;
                    if (!RandomCampaignStart.Settings.FullRandomMode)
                    {
                        // remove ancestral mech if specified
                        if (RemoveAncestralMech && firstrun)
                        {
                            __instance.ActiveMechs.Remove(0);
                        }

                        currentLanceWeight = 0;

                        while (currentLanceWeight < RandomCampaignStart.Settings.MinimumStartingWeight || currentLanceWeight > RandomCampaignStart.Settings.MaximumStartingWeight)
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
                                for (var i = baySlot; i < 6; i++)
                                {
                                    __instance.ActiveMechs.Remove(i);
                                }
                            }

                            //It's not a BUG, it's a FEATURE.
                            LanceCounter++;
                            if (LanceCounter > RandomCampaignStart.Settings.SpiderLoops)
                            {
                                MechDef mechDefSpider = new MechDef(__instance.DataManager.MechDefs.Get("mechdef_spider_SDR-5V"), __instance.GenerateSimGameUID(), true);
                                lance.Add(mechDefSpider); // worry about sorting later
                                for (int j = baySlot; j < 6; j++)
                                {
                                    __instance.AddMech(j, mechDefSpider, true, true, false, null);
                                }

                                break;
                            }

                            var legacyLance = new List<string>();
                            legacyLance.AddRange(GetRandomSubList(RandomCampaignStart.Settings.AssaultMechsPossible, RandomCampaignStart.Settings.NumberAssaultMechs));
                            legacyLance.AddRange(GetRandomSubList(RandomCampaignStart.Settings.HeavyMechsPossible, RandomCampaignStart.Settings.NumberHeavyMechs));
                            legacyLance.AddRange(GetRandomSubList(RandomCampaignStart.Settings.MediumMechsPossible, RandomCampaignStart.Settings.NumberMediumMechs));
                            legacyLance.AddRange(GetRandomSubList(RandomCampaignStart.Settings.LightMechsPossible, RandomCampaignStart.Settings.NumberLightMechs));

                            // check to see if we're on the last mechbay and if we have more mechs to add
                            // if so, store the mech at index 5 before next iteration.
                            for (int j = 0; j < legacyLance.Count; j++)
                            {
                                LogDebug("Build Lance");

                                MechDef mechDef2 = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[j]), __instance.GenerateSimGameUID(), true);
                                __instance.AddMech(baySlot, mechDef2, true, true, false, null);
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
                            if (currentLanceWeight >= RandomCampaignStart.Settings.MinimumStartingWeight && currentLanceWeight <= RandomCampaignStart.Settings.MaximumStartingWeight)
                            {
                                LogDebug("Classic Mode");
                                for (int y = 0; y < __instance.ActiveMechs.Count(); y++)
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
                        // full random mode
                        LogDebug("New mode");
                        int minLanceSize = RandomCampaignStart.Settings.MinimumLanceSize;
                        float maxWeight = RandomCampaignStart.Settings.MaximumStartingWeight;
                        float maxLanceSize = 6;
                        bool firstTargetRun = false;
                        __instance.ActiveMechs.Remove(0);

                        if (RemoveAncestralMech)
                        {
                            baySlot = 0;
                            currentLanceWeight = 0;
                            if (AncestralMechDef.Description.Id == "mechdef_centurion_TARGETDUMMY" && RandomCampaignStart.Settings.IgnoreAncestralMech)
                            {
                                maxLanceSize = RandomCampaignStart.Settings.MaximumLanceSize + 1;
                                firstTargetRun = true;
                                minLanceSize = minLanceSize + 1;
                            }
                        }
                        else if (!RemoveAncestralMech && RandomCampaignStart.Settings.IgnoreAncestralMech)
                        {
                            lance.Add(AncestralMechDef);
                            currentLanceWeight = 0;
                            maxLanceSize = RandomCampaignStart.Settings.MaximumLanceSize + 1;
                        }
                        else
                        {
                            baySlot = 1;
                            lance.Add(AncestralMechDef);
                            currentLanceWeight = AncestralMechDef.Chassis.Tonnage;
                        }

                        bool dupe = false;
                        bool excluded = false;
                        bool blacklisted = false;
                        bool TargetDummy = false;
                        while (minLanceSize > lance.Count || currentLanceWeight < RandomCampaignStart.Settings.MinimumStartingWeight)
                        {
                            #region Def listing loops

                            //Logger.LogDebug($"In while loop");
                            //foreach (var mech in __instance.DataManager.MechDefs)
                            //{
                            //    Logger.LogDebug($"K:{mech.Key} V:{mech.Value}");
                            //}
                            //foreach (var chasis in __instance.DataManager.ChassisDefs)
                            //{
                            //    Logger.LogDebug($"K:{chasis.Key}");
                            //}

                            #endregion

                            var randomMech = mechTonnages.ElementAt(rng.Next(0, mechTonnages.Count));
                            // getting chassisDefs so renaming the key to match mechDefs ID
                            var mechString = randomMech.Key.Replace("chassisdef", "mechdef");
                            var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(mechString), __instance.GenerateSimGameUID());

                            // skip mechs which are newer than the game's date
                            var mechExists = mechDef.MinAppearanceDate < UnityGameInstance.BattleTechGame.Simulation.CampaignStartDate;
                            LogDebug($"{mechDef.Chassis.VariantName} appears in: {mechDef.MinAppearanceDate.ToString()} ({(mechExists ? "Pass" : "Fail")})");

                            if (mechDef.MinAppearanceDate != null && !mechExists) continue;

                            // SPIDERS! It's not a BUG, it's a FEATURE.
                            if (LanceCounter > RandomCampaignStart.Settings.SpiderLoops)
                            {
                                MechDef mechDefSpider = new MechDef(__instance.DataManager.MechDefs.Get("mechdef_spider_SDR-5V"), __instance.GenerateSimGameUID(), true);
                                lance.Add(mechDefSpider); // worry about sorting later
                                for (int j = baySlot; j < 6; j++)
                                {
                                    __instance.AddMech(j, mechDefSpider, true, true, false, null);
                                }

                                break;
                            }

                            if (mechDef.MechTags.Contains("BLACKLISTED"))
                            {
                                currentLanceWeight = 0;
                                blacklisted = true;

                                //Logger.LogDebug($"Blacklisted! {mechDef.Name}");
                            }

                            //Logger.LogDebug($"TestMech {mechDef.Name}");
                            foreach (var mechID in RandomCampaignStart.Settings.ExcludedMechs)
                            {
                                if (mechID == mechDef.Description.Id)
                                {
                                    currentLanceWeight = 0;
                                    excluded = true;

                                    //Logger.LogDebug($"Excluded! {mechDef.Name}");
                                }
                            }

                            if (!RandomCampaignStart.Settings.AllowDuplicateChassis)
                            {
                                foreach (var mech in lance)
                                {
                                    if (mech.Name == mechDef.Name)
                                    {
                                        currentLanceWeight = 0;
                                        dupe = true;

                                        //Logger.LogDebug($"SAME SAME! {mech.Name}\t\t{mechDef.Name}");
                                    }
                                }
                            }

                            // does the mech fit into the lance?
                            if (TargetDummy)
                            {
                                TargetDummy = false;
                            }
                            else
                            {
                                currentLanceWeight = currentLanceWeight + mechDef.Chassis.Tonnage;
                            }

                            if (RandomCampaignStart.Settings.MaximumStartingWeight >= currentLanceWeight)
                            {
                                lance.Add(mechDef);

                                LogDebug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                                if (currentLanceWeight > RandomCampaignStart.Settings.MinimumStartingWeight + mechDef.Chassis.Tonnage)
                                    LogDebug("Minimum lance tonnage met:  done");

                                LogDebug($"current: {currentLanceWeight} tons. " +
                                         $"tonnage remaining: {RandomCampaignStart.Settings.MaximumStartingWeight - currentLanceWeight}. " +
                                         $"before lower limit hit: {Math.Max(0, RandomCampaignStart.Settings.MinimumStartingWeight - currentLanceWeight)}");
                            }

                            // invalid lance, reset
                            if (currentLanceWeight > RandomCampaignStart.Settings.MaximumStartingWeight || lance.Count > maxLanceSize || dupe || blacklisted || excluded || firstTargetRun)
                            {
                                //Logger.LogDebug($"Clearing invalid lance");
                                currentLanceWeight = 0;
                                lance.Clear();
                                dupe = false;
                                blacklisted = false;
                                excluded = false;
                                firstTargetRun = false;
                                LanceCounter++;
                                if (RemoveAncestralMech)
                                {
                                    baySlot = 0;
                                    currentLanceWeight = 0;
                                    maxLanceSize = RandomCampaignStart.Settings.MaximumLanceSize;
                                    if (AncestralMechDef.Description.Id == "mechdef_centurion_TARGETDUMMY" && RandomCampaignStart.Settings.IgnoreAncestralMech)
                                    {
                                        maxLanceSize = RandomCampaignStart.Settings.MaximumLanceSize + 1;
                                        TargetDummy = true;
                                    }
                                }
                                else if (!RemoveAncestralMech && RandomCampaignStart.Settings.IgnoreAncestralMech)
                                {
                                    maxLanceSize = RandomCampaignStart.Settings.MaximumLanceSize + 1;
                                    currentLanceWeight = 0;
                                    lance.Add(AncestralMechDef);
                                    baySlot = 1;
                                }
                                else
                                {
                                    maxLanceSize = RandomCampaignStart.Settings.MaximumLanceSize;
                                    currentLanceWeight = AncestralMechDef.Chassis.Tonnage;
                                    lance.Add(AncestralMechDef);
                                    baySlot = 1;
                                }
                            }
                        }

                        LogDebug("Starting lance instantiation");
                        float tonnagechecker = 0;
                        for (int x = 0; x < lance.Count; x++)
                        {
                            LogDebug($"mech {x} is {lance[x].Name}");
                            __instance.AddMech(x, lance[x], true, true, false);
                            tonnagechecker = tonnagechecker + lance[x].Chassis.Tonnage;
                        }

                        LogDebug($"{tonnagechecker}");
                        float Maxtonnagedifference = tonnagechecker - RandomCampaignStart.Settings.MaximumStartingWeight;
                        float Mintonnagedifference = tonnagechecker - RandomCampaignStart.Settings.MinimumStartingWeight;
                        LogDebug($"Over tonnage Maximum amount: {Maxtonnagedifference}");
                        LogDebug($"Over tonnage Minimum amount: {Mintonnagedifference}");
                        lance.Clear();
                        // valid lance created
                    }
                }
            }
        }
    }

    internal class ModSettings
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
    }
}