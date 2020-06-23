using System;
using System.Collections.Generic;
using System.Reflection;
using BattleTech;
using Harmony;
using Newtonsoft.Json;
using System.Linq;

// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global


namespace RandomCareerStart
{
    [HarmonyPatch(typeof(SimGameState), "AddCareerMechs")]
    public static class SimGameState_AddCareerMechs_Patch
    {
        public static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(SimGameState), "FirstTimeInitializeDataFromDefs")]
    public static class SimGameState_FirstTimeInitializeDataFromDefs_Patch
    {
        // from https://stackoverflow.com/questions/273313/randomize-a-listt
        private static readonly Random rng = new Random();

        private static void RNGShuffle<T>(this IList<T> list)
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

        private static List<T> GetRandomSubList<T>(List<T> list, int number)
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

        public static void Postfix(SimGameState __instance)
        {
            if (RngStart.Settings.NumberRandomRonin + RngStart.Settings.NumberProceduralPilots + RngStart.Settings.NumberRoninFromList > 0)
            {
                RandomizeRonin(__instance);
            }

            Logger.Debug($"Starting lance creation {RngStart.Settings.MinimumStartingWeight} - {RngStart.Settings.MaximumStartingWeight} tons");
            // mechs
            if (RngStart.Settings.UseRandomMechs)
            {
                // memoize dictionary of tonnages since we may be looping a lot
                Logger.Debug($"Memoizing");

                var AncestralMechDef = __instance.Constants.CareerMode.StartingPlayerMech;
                var lance = new List<string>();
                //var lance = new List<MechDef>();
                float currentLanceWeight = 0;
                var mechTonnages = new Dictionary<string, float>();

                Logger.Debug($"A");
                var startDate = __instance.GetCampaignStartDate();
                //Trim the lists.
                foreach (var kvp in __instance.DataManager.ChassisDefs)
                {
                    var mechDefId = kvp.Key.Replace("chassisdef", "mechdef");
                    if (!__instance.DataManager.Exists(BattleTechResourceType.MechDef, mechDefId))
                    {
                        continue;
                    }

                    var minAppearanceDate = __instance.DataManager.MechDefs.Get(mechDefId).MinAppearanceDate;
                    if (minAppearanceDate.HasValue && minAppearanceDate > startDate)
                    {
                        continue;
                    }
                    
                    if (kvp.Key.Contains("DUMMY") && !kvp.Key.Contains("CUSTOM"))
                    {
                        // just in case someone calls their mech DUMMY
                        continue;
                    }
                    if (kvp.Key.Contains("CUSTOM") || kvp.Key.Contains("DUMMY"))
                        continue;
                    
                    if (RngStart.Settings.MaximumMechWeight != 100)
                        if (kvp.Value.Tonnage > RngStart.Settings.MaximumMechWeight || kvp.Value.Tonnage < 20)
                            continue;
                    // passed checks, add to Dictionary
                    mechTonnages.Add(kvp.Key, kvp.Value.Tonnage);
                }
                Logger.Debug($"B");
                for (int xloop = 0; xloop < RngStart.Settings.Loops; xloop++)
                {
                    Logger.Debug($"C");
                    int minLanceSize = RngStart.Settings.MinimumLanceSize;
                    float maxWeight = RngStart.Settings.MaximumStartingWeight;
                    float maxLanceSize = RngStart.Settings.MaximumLanceSize;
                    bool firstTargetRun = false;

                    var randomStarterMech = mechTonnages.ElementAt(rng.Next(0, mechTonnages.Count));
                    var StartermechString = randomStarterMech.Key.Replace("chassisdef", "mechdef");
                    var StarterMechTonnage = randomStarterMech.Value;
                    Logger.Debug($"D");
                    Logger.Debug(StartermechString);
                    Logger.Debug(AncestralMechDef);
                    if (AncestralMechDef == "mechdef_centurion_TARGETDUMMY")
                    {
                        lance.Add(StartermechString);
                        currentLanceWeight = randomStarterMech.Value;
                    }
                    else
                    {
                        lance.Add(AncestralMechDef);
                        StartermechString = AncestralMechDef;
                        Logger.Debug("Here, right?");
                        currentLanceWeight = __instance.DataManager.MechDefs.Get(AncestralMechDef).Chassis.Tonnage;
                        StarterMechTonnage = currentLanceWeight;
                    }

                    Logger.Debug($"E");

                    int LanceCounter = 1;

                    // cap the lance tonnage
                    
                    //__instance.ActiveMechs.Remove(0);

                    bool dupe = false;
                    bool excluded = false;
                    bool blacklisted = false;
                    int RVMechCount = 0;
                    int MediumMechCount = 0;
                    int GhettoCount = 0;

                    while (minLanceSize > lance.Count || currentLanceWeight < RngStart.Settings.MinimumStartingWeight)
                    {
                        Logger.Debug($"F");
                        #region Def listing loops

                        //Logger.Debug($"In while loop");
                        //foreach (var mech in __instance.DataManager.MechDefs)
                        //{
                        //    Logger.Debug($"K:{mech.Key} V:{mech.Value}");
                        //}
                        //foreach (var chasis in __instance.DataManager.ChassisDefs)
                        //{
                        //    Logger.Debug($"K:{chasis.Key}");
                        //}
                        #endregion
                        // build lance collection from dictionary for speed

                        var randomMech = mechTonnages.ElementAt(rng.Next(0, mechTonnages.Count));
                        var mechString = randomMech.Key.Replace("chassisdef", "mechdef");

                        // getting chassisdefs so renaming the key to match mechdefs Id
                        var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(mechString), __instance.GenerateSimGameUID());
                        //It's not a BUG, it's a FEATURE.
                        if (LanceCounter > RngStart.Settings.SpiderLoops)
                        {
                            MechDef mechDefSpider = new MechDef(__instance.DataManager.MechDefs.Get("mechdef_spider_SDR-5V"), __instance.GenerateSimGameUID(), true);
                            lance.Add(mechDefSpider.Description.Id); // worry about sorting later
                            Traverse.Create(__instance).Method("AddMechs", new Type[] { typeof(List<string>) }).GetValue(lance);
                            //for (int j = baySlot; j < 6; j++)
                            //{
                            //    __instance.AddMech(j, mechDefSpider, true, true, false, null);
                            //}
                            break;
                        }
                        Logger.Debug($"G");

                        if (mechDef.MechTags.Contains("BLACKLISTED"))
                        {
                            currentLanceWeight = 0;
                            blacklisted = true;

                            //Logger.Debug($"Blacklisted! {mechDef.Name}");
                        }

                        Logger.Debug($"TestMech {mechDef.Name}");
                        foreach (var mechID in RngStart.Settings.ExcludedMechs)
                        {
                            if (mechID == mechDef.Description.Id)
                            {
                                currentLanceWeight = 0;
                                excluded = true;

                                Logger.Debug($"Excluded! {mechDef.Name}");
                            }
                        }


                        if (!RngStart.Settings.AllowDuplicateChassis)
                        {
                            foreach (var mech in lance)
                            {
                                Logger.Debug("Mech Chassis Comparer");
                                Logger.Debug(mech.Substring(0, 12) + ":" + mechDef.Description.Id.Substring(0, 12));
                                if (mech.Substring(0,12) == mechDef.Description.Id.Substring(0,12))
                                {
                                    currentLanceWeight = 0;
                                    dupe = true;
                                    Logger.Debug($"SAME SAME!");
                                }
                            }
                        }
                        if (mechDef.Description.UIName.Contains("-RV") && RngStart.Settings.LimitRVMechs)
                            RVMechCount++;

                        if (mechDef.Chassis.weightClass == WeightClass.MEDIUM && RngStart.Settings.ForceSingleMedium)
                            MediumMechCount++;

                        if (mechDef.Chassis.Tonnage == 20)
                            GhettoCount++;

                        // does the mech fit into the lance?
                        
                        currentLanceWeight = currentLanceWeight + mechDef.Chassis.Tonnage;

                        if (RngStart.Settings.MaximumStartingWeight >= currentLanceWeight)
                        {

                            lance.Add(mechDef.Description.Id);

                            Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            if (currentLanceWeight > RngStart.Settings.MinimumStartingWeight + mechDef.Chassis.Tonnage)
                            Logger.Debug($"Minimum lance tonnage met:  done");

                            Logger.Debug($"current: {currentLanceWeight} tons. " +
                                $"tonnage remaining: {RngStart.Settings.MaximumStartingWeight - currentLanceWeight}. " +
                                $"before lower limit hit: {Math.Max(0, RngStart.Settings.MinimumStartingWeight - currentLanceWeight)}");
                        }

                        // invalid lance, reset
                        if (currentLanceWeight > RngStart.Settings.MaximumStartingWeight || lance.Count > maxLanceSize || dupe || blacklisted || excluded || firstTargetRun || RVMechCount > 1 || (lance.Count >= minLanceSize && MediumMechCount != 1) || GhettoCount > 1)
                        {
                            Logger.Debug($"Clearing invalid lance");
                            currentLanceWeight = StarterMechTonnage;
                            lance.Clear();
                            lance.Add(StartermechString);
                            dupe = false;
                            blacklisted = false;
                            excluded = false;
                            firstTargetRun = false;
                            RVMechCount = 0;
                            LanceCounter++;
                            if (StarterMechTonnage > 35)
                                MediumMechCount = 1;
                            else
                                MediumMechCount = 0;
                            GhettoCount = 0;
                            continue;
                        }

                        Logger.Debug($"Done a loop");
                    }
                    Logger.Debug($"New mode");
                    Logger.Debug($"Starting lance instantiation");

                    float tonnagechecker = 0;
                    Traverse.Create(__instance).Method("AddMechs", new Type[] { typeof(List<string>) }).GetValue(lance);

                    Logger.Debug($"{tonnagechecker}");
                    float Maxtonnagedifference = currentLanceWeight - RngStart.Settings.MaximumStartingWeight;
                    float Mintonnagedifference = currentLanceWeight - RngStart.Settings.MinimumStartingWeight;
                    Logger.Debug($"Over tonnage Maximum amount: {Maxtonnagedifference}");
                    Logger.Debug($"Over tonnage Minimum amount: {Mintonnagedifference}");
                    lance.Clear();
                    // valid lance created
                }
            }
        }


        [HarmonyPatch(typeof(SimGameState), "_OnDefsLoadComplete")]
        public static class Initialize_New_Game
        {
            public static void Postfix(SimGameState __instance)
            {
                float cost = 0;
                foreach (MechDef mechdef in __instance.ActiveMechs.Values)
                {
                    cost += mechdef.Description.Cost * RngStart.Settings.MechPercentageStartingCost/100;
                }
                __instance.AddFunds(-(int)cost, null, false);
            }
        }

        //Code for randomizing starting pilots
        public static void RandomizeRonin(SimGameState sim)
        {
            while (sim.PilotRoster.Count > 0)
            {
                sim.PilotRoster.RemoveAt(0);
            }
            List<PilotDef> list = new List<PilotDef>();

            if (RngStart.Settings.StartingRonin != null)
            {
                var RoninRandomizer = new List<string>();
                RoninRandomizer.AddRange(GetRandomSubList(RngStart.Settings.StartingRonin, RngStart.Settings.NumberRoninFromList));
                foreach (var roninID in RoninRandomizer)
                {
                    var pilotDef = sim.DataManager.PilotDefs.Get(roninID);

                    // add directly to roster, don't want to get duplicate ronin from random ronin
                    if (pilotDef != null)
                        sim.AddPilotToRoster(pilotDef, true, true);
                }
            }

            if (RngStart.Settings.NumberRandomRonin > 0)
            {
                List<PilotDef> list2 = new List<PilotDef>(sim.RoninPilots);
                for (int m = list2.Count - 1; m >= 0; m--)
                {
                    for (int n = 0; n < sim.PilotRoster.Count; n++)
                    {
                        if (list2[m].Description.Id == sim.PilotRoster[n].Description.Id)
                        {
                            list2.RemoveAt(m);
                            break;
                        }
                    }
                }
                list2.RNGShuffle<PilotDef>();
                for (int i = 0; i < RngStart.Settings.NumberRandomRonin; i++)
                {
                    list.Add(list2[i]);
                }
            }

            if (RngStart.Settings.NumberProceduralPilots > 0)
            {
                List<PilotDef> list3 = new List<PilotDef>();
                int f = 0;
                while (f < RngStart.Settings.NumberProceduralPilots)
                {
                    PilotDef pilotDef = sim.PilotGenerator.GeneratePilots(1, 1, 0f, out list3)[0];
                    if (sim.CanPilotBeCareerModeStarter(pilotDef))
                    {
                        pilotDef.SetDayOfHire(sim.DaysPassed);
                        sim.AddPilotToRoster(pilotDef, false, true);
                        f++;
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

            public bool ChooseStartingMech = false;
            public bool IgnoreStartingMech = true;

            public string ModDirectory = string.Empty;
            public bool Debug = false;
            public int SpiderLoops = 1000;
            public int Loops = 1;

            public bool UseRandomMechs = true;
            public bool LimitRVMechs = true;
            public bool ForceSingleMedium = true;

        }

        public static class RngStart
        {
            internal static ModSettings Settings;

            public static void Init(string modDir, string modSettings)
            {
                var harmony = HarmonyInstance.Create("io.github.mpstark.RandomCareerStart");
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
            }
        }
    }
}