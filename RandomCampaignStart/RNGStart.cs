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

namespace RandomCampaignStart
{
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
                while (__instance.PilotRoster.Count > 0)
                {
                    __instance.PilotRoster.RemoveAt(0);
                }
                List<PilotDef> list = new List<PilotDef>();

                if (RngStart.Settings.StartingRonin != null)
                {
                    var RoninRandomizer = new List<string>();
                    RoninRandomizer.AddRange(GetRandomSubList(RngStart.Settings.StartingRonin, RngStart.Settings.NumberRoninFromList));
                    foreach (var roninID in RoninRandomizer)
                    {
                        var pilotDef = __instance.DataManager.PilotDefs.Get(roninID);

                        // add directly to roster, don't want to get duplicate ronin from random ronin
                        if (pilotDef != null)
                            __instance.AddPilotToRoster(pilotDef, true);
                    }
                }

                if (RngStart.Settings.NumberRandomRonin > 0)
                {
                    List<PilotDef> list2 = new List<PilotDef>(__instance.RoninPilots);
                    list2.RNGShuffle<PilotDef>();
                    for (int i = 0; i < RngStart.Settings.NumberRandomRonin; i++)
                    {
                        list.Add(list2[i]);
                    }
                }
                if (RngStart.Settings.NumberProceduralPilots > 0)
                {
                    List<PilotDef> list3;
                    List<PilotDef> collection = __instance.PilotGenerator.GeneratePilots(RngStart.Settings.NumberProceduralPilots, 1, 0f, out list3);
                    list.AddRange(collection);
                }
                foreach (PilotDef def in list)
                {
                    __instance.AddPilotToRoster(def, true);
                }
            }

            Logger.Debug($"Starting lance creation {RngStart.Settings.MinimumStartingWeight} - {RngStart.Settings.MaximumStartingWeight} tons");
            // mechs
            var lance = new List<MechDef>();
            float currentLanceWeight = 0;
            //int x = 0;
            var baySlot = 1;

            // clear the initial lance
            for (var i = 1; i < __instance.Constants.Story.StartingLance.Length + 1; i++)
                __instance.ActiveMechs.Remove(i);


            // memoize dictionary of tonnages since we may be looping a lot
            //Logger.Debug($"Memoizing");
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
                if (RngStart.Settings.MaximumMechWeight != 100)
                {

                    if (kvp.Value.Tonnage > RngStart.Settings.MaximumMechWeight)
                    {
                        continue;
                    }
                }
                // passed checks, add to Dictionary
                mechTonnages.Add(kvp.Key, kvp.Value.Tonnage);
            }
            //Logger.Debug($"Done memoizing");
            if (!RngStart.Settings.FullRandomMode)
            {
                // remove ancestral mech if specified
                if (RngStart.Settings.RemoveAncestralMech)
                {
                    __instance.ActiveMechs.Remove(0);
                }
                currentLanceWeight = 0;
                bool firstrun = true;

                while (currentLanceWeight < RngStart.Settings.MinimumStartingWeight || currentLanceWeight > RngStart.Settings.MaximumStartingWeight)
                {
                    if (RngStart.Settings.RemoveAncestralMech)
                    {
                        currentLanceWeight = 0;
                        baySlot = 0;
                    }
                    else
                    {
                        currentLanceWeight = 45;
                        baySlot = 1;
                    }
                    if (!firstrun)
                    {
                        for (var i = baySlot; i < __instance.Constants.Story.StartingLance.Length + 1; i++)
                        {
                            __instance.ActiveMechs.Remove(i);
                        }
                    }
                    var legacyLance = new List<string>();
                    legacyLance.AddRange(GetRandomSubList(RngStart.Settings.AssaultMechsPossible, RngStart.Settings.NumberAssaultMechs));
                    legacyLance.AddRange(GetRandomSubList(RngStart.Settings.HeavyMechsPossible, RngStart.Settings.NumberHeavyMechs));
                    legacyLance.AddRange(GetRandomSubList(RngStart.Settings.MediumMechsPossible, RngStart.Settings.NumberMediumMechs));
                    legacyLance.AddRange(GetRandomSubList(RngStart.Settings.LightMechsPossible, RngStart.Settings.NumberLightMechs));

                    // check to see if we're on the last mechbay and if we have more mechs to add
                    // if so, store the mech at index 5 before next iteration.
                    for (int j = 0; j < legacyLance.Count; j++)
                    {
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
                        currentLanceWeight += (int)mechDef2.Chassis.Tonnage;
                    }
                    firstrun = false;
                }
            }
            else  // G new mode
            {
                Logger.Debug($"New mode");
                __instance.ActiveMechs.Remove(0);
                baySlot = 0;

                // cap the lance tonnage
                float maxWeight = Math.Min(100 * RngStart.Settings.MaximumLanceSize, RngStart.Settings.MaximumStartingWeight);
                float maxLanceSize = Math.Min(6, RngStart.Settings.MaximumLanceSize);

                // loop until we have 4-6 mechs

                // if the lance weights 
                // if the number of mechs is between 4 and 6.  or settings

                while (RngStart.Settings.MinimumLanceSize > lance.Count || currentLanceWeight < RngStart.Settings.MinimumStartingWeight)
                {
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
                    // TODO only when lance is valid do we instantiate it
                    var randomMech = mechTonnages.ElementAt(rng.Next(0, mechTonnages.Count));
                    var mechString = randomMech.Key.Replace("chassisdef", "mechdef");  
                    // getting chassisdefs so renaming the key to match mechdefs Id
                    //var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(mechString), __instance.GenerateSimGameUID());
                    var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(mechString), __instance.GenerateSimGameUID());

                    
                    if(mechDef.MechTags.Contains("BLACKLISTED"))
                    {
                        Logger.Debug($"Blacklisted! {mechDef.Name}");
                        currentLanceWeight = RngStart.Settings.MaximumStartingWeight + 5;
                    }

                    foreach (var mechID in RngStart.Settings.ExcludedMechs)
                    {
                        if(mechID == mechDef.Description.Id)
                        {
                            Logger.Debug($"Excluded! {mechDef.Name}");
                            currentLanceWeight = RngStart.Settings.MaximumStartingWeight + 5;
                        }
                    }


                    if (!RngStart.Settings.AllowDuplicateChassis)
                    {
                        bool dupe = false;
                        foreach (var mech in lance)
                        {
                            if (mech.Name == mechDef.Name)
                            {
                                Logger.Debug($"SAME SAME! {mech.Name}\t\t{mechDef.Name}");
                                currentLanceWeight = 0;
                                dupe = true;
                            }
                        }
                        if (dupe == true)
                            lance.Clear();
                    }


                    // does the mech fit into the lance?

                    currentLanceWeight = currentLanceWeight + mechDef.Chassis.Tonnage;
                    if (RngStart.Settings.MaximumStartingWeight >= currentLanceWeight)
                    {
                        Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                        lance.Add(mechDef); // worry about sorting later

                        if (currentLanceWeight > RngStart.Settings.MinimumStartingWeight + mechDef.Chassis.Tonnage)
                            Logger.Debug($"Minimum lance tonnage met:  done");

                        Logger.Debug($"current: {currentLanceWeight} tons. " +
                            $"tonnage remaining: {RngStart.Settings.MaximumStartingWeight - currentLanceWeight}. " +
                            $"before lower limit hit: {Math.Max(0, RngStart.Settings.MinimumStartingWeight - currentLanceWeight)}");
                    }
                    // invalid lance, reset
                    if (currentLanceWeight > RngStart.Settings.MaximumStartingWeight || lance.Count > maxLanceSize)
                    {
                        Logger.Debug($"Clearing invalid lance");
                        currentLanceWeight = 0;
                        lance.Clear();
                        continue;
                    }
                    


                    //Logger.Debug($"Done a loop");
                }
                Logger.Debug($"Starting lance instantiation");
                for (int x = 0; x < lance.Count; x++)
                {
                    Logger.Debug($"x is {x} and lance[x] is {lance[x].Name}");
                    __instance.AddMech(x, lance[x], true, true, false);
                }
                // valid lance created
            }
        }
        // TODO apply back to legacy mode
        //__instance.AddMech(baySlot, mechDef, true, true, false);

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

            public List<string> StartingRonin = new List<string>();
            public int NumberRoninFromList = 4;
            public List<string> ExcludedMechs = new List<string>();

            public int NumberProceduralPilots = 0;
            public int NumberRandomRonin = 4;

            public bool RemoveAncestralMech = false;

            public string ModDirectory = string.Empty;
            public bool Debug = false;
        }

        public static class RngStart
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
            }
        }
    }
}