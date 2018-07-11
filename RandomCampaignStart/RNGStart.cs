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

            /*var simgame = __instance;
            foreach (KeyValuePair<string, ChassisDef> dataManagerChassisDef in simgame.DataManager.ChassisDefs)
            {

                Logger.Debug($"storage key: {dataManagerChassisDef.Key}");
                Logger.Debug($"chassis id: {dataManagerChassisDef.Value.Description.Id}");
                Logger.Debug($"chassis variant: {dataManagerChassisDef.Value.VariantName}");
            }*/

            if (RngStart.Settings.NumberRandomRonin + RngStart.Settings.NumberProceduralPilots > 0)
            {
                // clear roster
                while (__instance.PilotRoster.Count > 0)
                    __instance.PilotRoster.RemoveAt(0);

                // pilotgenerator seems to give me the same exact results for ronin
                // every time, and can push out duplicates, which is odd?
                // just do our own thing
                var pilots = new List<PilotDef>();

                if (RngStart.Settings.StartingRonin != null)
                {
                    foreach (var roninID in RngStart.Settings.StartingRonin)
                    {
                        var pilotDef = __instance.DataManager.PilotDefs.Get(roninID);

                        // add directly to roster, don't want to get duplicate ronin from random ronin
                        if (pilotDef != null)
                            __instance.AddPilotToRoster(pilotDef, true);
                    }
                }

                pilots.AddRange(GetRandomSubList(__instance.RoninPilots, RngStart.Settings.NumberRandomRonin));

                // pilot generator works fine for non-ronin =/
                if (RngStart.Settings.NumberProceduralPilots > 0)
                    pilots.AddRange(__instance.PilotGenerator.GeneratePilots(RngStart.Settings.NumberProceduralPilots, 1, 0, out _));

                // actually add the pilots to the SimGameState
                pilots.Select(x => __instance.AddPilotToRoster(x, true));

            }

            Logger.Debug($"Starting lance creation {RngStart.Settings.MinimumStartingWeight} - {RngStart.Settings.MaximumStartingWeight} tons");
            // mechs
            if (RngStart.Settings.MinimumLanceSize > 0 ||
                RngStart.Settings.NumberLightMechs + RngStart.Settings.NumberMediumMechs +
                RngStart.Settings.NumberHeavyMechs + RngStart.Settings.NumberAssaultMechs > 0)
            {
                var lance = new List<MechDef>();
                var legacyLance = new List<string>();
                float currentLanceWeight = 0;
                //int x = 0;
                var baySlot = 1;

                // clear the initial lance
                for (var i = 1; i < __instance.Constants.Story.StartingLance.Length + 1; i++)
                    __instance.ActiveMechs.Remove(i);

                // remove ancestral mech if specified
                if (RngStart.Settings.RemoveAncestralMech)
                {
                    __instance.ActiveMechs.Remove(0);
                    baySlot = 0;
                }

                // memoize dictionary of tonnages since we may be looping a lot
                //Logger.Debug($"Memoizing");
                var mechTonnages = new Dictionary<string, float>();
                foreach (var kvp in __instance.DataManager.ChassisDefs)
                {
                    if (!RngStart.Settings.AllowCustomMechs)
                        //Logger.Debug($"{kvp.Key}");
                    if (kvp.Key.Contains("DUMMY") && !kvp.Key.Contains("CUSTOM")) // just in case someone calls their mech DUMMY
                        continue;
                    if (kvp.Key.Contains("CUSTOM") || kvp.Key.Contains("DUMMY"))
                        continue;

                    // passed checks, add to Dictionary
                    mechTonnages.Add(kvp.Key, kvp.Value.Tonnage);
                }
                //Logger.Debug($"Done memoizing");
                if (RngStart.Settings.NumberAssaultMechs + RngStart.Settings.NumberHeavyMechs +
                    RngStart.Settings.NumberMediumMechs + RngStart.Settings.NumberLightMechs > 0)
                {
                    Logger.Debug($"Legacy mode");
                    legacyLance.AddRange(GetRandomSubList(RngStart.Settings.AssaultMechsPossible, RngStart.Settings.NumberAssaultMechs));
                    legacyLance.AddRange(GetRandomSubList(RngStart.Settings.HeavyMechsPossible, RngStart.Settings.NumberHeavyMechs));
                    legacyLance.AddRange(GetRandomSubList(RngStart.Settings.MediumMechsPossible, RngStart.Settings.NumberMediumMechs));
                    legacyLance.AddRange(GetRandomSubList(RngStart.Settings.LightMechsPossible, RngStart.Settings.NumberLightMechs));
                    for (var i = 0; i < legacyLance.Count; i++)
                    {
                        var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[i]), __instance.GenerateSimGameUID());
                        __instance.AddMech(baySlot, mechDef, true, true, false);
                        // check to see if we're on the last mechbay and if we have more mechs to add
                        // if so, store the mech at index 5 before next iteration.
                        if (baySlot == 5 && i + 1 < legacyLance.Count)
                            __instance.UnreadyMech(5, mechDef);
                        else
                            baySlot++;
                    }
                }
                else  // G new mode
                {
                    Logger.Debug($"New mode");

                    // cap the lance tonnage
                    float maxWeight = Math.Min(400, RngStart.Settings.MaximumStartingWeight);

                    // loop until we have 4-6 mechs

                    // if the lance weights 
                    // if the number of mechs is between 4 and 6.  or settings

                    while (currentLanceWeight <= RngStart.Settings.MinimumStartingWeight &&
                           __instance.ActiveMechs.Count < 7 &&
                           RngStart.Settings.MinimumLanceSize >= __instance.ActiveMechs.Count)
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
                        var randomMech = __instance.DataManager.ChassisDefs.ElementAt(rng.Next(0, __instance.DataManager.ChassisDefs.Count));

                        //var randomMech = mechTonnages.ElementAt(rng.Next(0, __instance.DataManager.ChassisDefs.Count));
                        var mechString = randomMech.Key.Replace("chassisdef", "mechdef");  // getting chassisdefs so renaming the key to match mechdefs Id
                        var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(mechString), __instance.GenerateSimGameUID());

                        // does the mech fit into the lance?
                        if (RngStart.Settings.MaximumStartingWeight >= currentLanceWeight + mechDef.Chassis.Tonnage)
                        {
                            Logger.Debug($"Adding mech {mechString} {mechDef.Chassis.Tonnage} tons");
                            lance.Add(mechDef); // worry about sorting later
                            currentLanceWeight += mechDef.Chassis.Tonnage;

                            if (currentLanceWeight > RngStart.Settings.MinimumStartingWeight + mechDef.Chassis.Tonnage)
                                Logger.Debug($"Minimum lance tonnage met:  done");

                            Logger.Debug($"current: {currentLanceWeight} tons. " +
                                $"tonnage remaining: {RngStart.Settings.MaximumStartingWeight - currentLanceWeight}. " +
                                $"before lower limit hit: {Math.Max(0, RngStart.Settings.MinimumStartingWeight - currentLanceWeight)}");
                        }
                        // invalid lance, reset
                        else if (lance.Count < 4 &&
                                 currentLanceWeight >= RngStart.Settings.MinimumStartingWeight)
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
            public float MaximumMechWeight = 50;  // not implemented
            public int MinimumLanceSize = 4;
            public bool AllowCustomMechs = false;

            public List<string> StartingRonin = new List<string>();

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
