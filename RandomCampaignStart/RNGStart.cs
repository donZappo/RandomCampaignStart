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

            // mechs
            if (RngStart.Settings.MinimumLanceSize > 0 ||
                RngStart.Settings.NumberLightMechs + RngStart.Settings.NumberMediumMechs +
                RngStart.Settings.NumberHeavyMechs + RngStart.Settings.NumberAssaultMechs > 0)
            {
                var lance = new List<string>();
                var legacyLance = new List<string>();
                //lance.AddRange(__instance.DataManager.ChassisDefs.Keys);
                float currentLanceWeight = 0;
                int x = 0;
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
                var mechTonnages = new Dictionary<string, float>();
                foreach (var kvp in __instance.DataManager.ChassisDefs)
                {
                    mechTonnages.Add(kvp.Key, kvp.Value.Tonnage);
                }

                if (RngStart.Settings.NumberAssaultMechs + RngStart.Settings.NumberHeavyMechs +
                    RngStart.Settings.NumberMediumMechs + RngStart.Settings.NumberLightMechs > 0)
                {
                    legacyLance.AddRange(GetRandomSubList(RngStart.Settings.AssaultMechsPossible, RngStart.Settings.NumberAssaultMechs));
                    legacyLance.AddRange(GetRandomSubList(RngStart.Settings.HeavyMechsPossible, RngStart.Settings.NumberHeavyMechs));
                    legacyLance.AddRange(GetRandomSubList(RngStart.Settings.MediumMechsPossible, RngStart.Settings.NumberMediumMechs));
                    legacyLance.AddRange(GetRandomSubList(RngStart.Settings.LightMechsPossible, RngStart.Settings.NumberLightMechs));

                        var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(mechIds[i]), __instance.GenerateSimGameUID());
                        __instance.AddMech(baySlot, mechDef, true, true, false);
                        // check to see if we're on the last mechbay and if we have more mechs to add
                        // if so, store the mech at index 5 before next iteration.
                        if (baySlot == 5 && i + 1 < mechIds.Count)
                            __instance.UnreadyMech(5, mechDef);
                        else
                            baySlot++;

                }
                else
                {
                    while (lance.Count < RngStart.Settings.MinimumLanceSize)
                    {
                        var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(lance[x]), __instance.GenerateSimGameUID());
                        Logger.Debug($"mechDef: {mechDef}");
                        if (RngStart.Settings.MaximumStartingWeight < currentLanceWeight + mechDef.Chassis.Tonnage)
                        {
                            __instance.AddMech(baySlot, mechDef, true, true, false);
                            currentLanceWeight += mechDef.Chassis.Tonnage;
                            baySlot++;
                        }

                        // check to see if we're on the last mechbay and if we have more mechs to add
                        // if so, store the mech at index 5 before next iteration.
                        if (baySlot == 5 && x + 1 < lance.Count)
                            __instance.UnreadyMech(5, mechDef);
                        else
                            baySlot++;
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

            public float MinimumStartingWeight = 200;
            public float MaximumStartingWeight = 300;
            public int MinimumLanceSize = 4;

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
