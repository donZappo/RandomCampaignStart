using System;
using System.Collections.Generic;
using System.Reflection;
using BattleTech;
using Harmony;
using Newtonsoft.Json;
using System.Linq;
using System.Text;
using HBS.DebugConsole;



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

            if (RngStart.Settings.NumberRandomRonin + RngStart.Settings.NumberProceduralPilots > 0)
            {
                while (__instance.PilotRoster.Count > 0)
                {
                    __instance.PilotRoster.RemoveAt(0);
                }

                List<PilotDef> list = new List<PilotDef>();
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
                    List<PilotDef> collection =
                        __instance.PilotGenerator.GeneratePilots(RngStart.Settings.NumberProceduralPilots, 1, 0f,
                            out list3);
                    list.AddRange(collection);
                }

                foreach (PilotDef def in list)
                {
                    __instance.AddPilotToRoster(def, true);
                }
            }

            Logger.Debug($"START {RngStart.Settings.MinimumStartingWeight} - {RngStart.Settings.MaximumStartingWeight} tons");
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
                while (currentLanceWeight < RngStart.Settings.MinimumStartingWeight ||
                       currentLanceWeight > RngStart.Settings.MaximumStartingWeight)
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
                        MechDef mechDef2 = new MechDef(__instance.DataManager.MechDefs.Get(legacyLance[j]),
                            __instance.GenerateSimGameUID(), true);
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
            else // random mode
            {
                for (int i = 0; i < RngStart.Settings.Loops; i++)
                {
                    Logger.Debug($"New mode");
                    __instance.ActiveMechs.Remove(0);

                    // cap the lance tonnage
                    float maxWeight = Math.Min(100 * RngStart.Settings.MaximumLanceSize, RngStart.Settings.MaximumStartingWeight);
                    float minWeight = Math.Max(20 * RngStart.Settings.MinimumLanceSize, RngStart.Settings.MinimumStartingWeight);
                    float maxLanceSize = Math.Min(6, RngStart.Settings.MaximumLanceSize);

                    while (RngStart.Settings.MinimumLanceSize > lance.Count ||
                           currentLanceWeight < minWeight)
                    {
                        #region Dev debug

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

                        bool dupe = false;

                        // build lance collection from dictionary for speed
                        var randomMech = mechTonnages.ElementAt(rng.Next(0, mechTonnages.Count));

                        // getting chassisdefs so renaming the key to match mechdefs Id
                        var mechString = randomMech.Key.Replace("chassisdef","mechdef"); 
                        var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(mechString), __instance.GenerateSimGameUID());

                        // does the mech fit into the lance?
                        currentLanceWeight = currentLanceWeight + mechDef.Chassis.Tonnage;
                        if (!(maxWeight >= currentLanceWeight))
                        {
                            continue;
                        }
                        if (RngStart.Settings.AllowDuplicateChassis)
                        {
                            foreach (var mech in lance)
                            {
                                if (mech.Name != mechDef.Name)
                                {
                                    continue;
                                }
                                Logger.Debug($"SAME SAME! {mech.Name}\t\t{mechDef.Name}");
                                dupe = true;
                                break;
                            }
                        }

                        if (dupe) continue;
                        lance.Add(mechDef);
                        if (currentLanceWeight > minWeight + mechDef.Chassis.Tonnage)
                        {
                            Logger.Debug($"Minimum lance tonnage met: Done");

                        }

                        Logger.Debug($"Adding {mechString}: {mechDef.Chassis.Tonnage} tons (now {currentLanceWeight}). " +
                                     $"{maxWeight - currentLanceWeight} tons remaining, " +
                                     $"{Math.Max(0, minWeight - currentLanceWeight)} before lower limit hit.");

                        // invalid lance, reset
                        if (currentLanceWeight > maxWeight || lance.Count > maxLanceSize)
                        {
                            Logger.Debug($"Clearing invalid lance");
                            currentLanceWeight = 0;
                            lance.Clear();
                        }
                    }

                    var sb = new StringBuilder();
                    for (int x = 0; x < lance.Count; x++)
                    {
                        sb.Append($"{lance[x].Name} ");
                    }
                    Logger.Debug($"Lance built: {sb}");

                    if (RngStart.Settings.Loops > 1)
                    {
                        currentLanceWeight = 0;
                        lance.Clear();
                        Logger.Debug("---------------------------------------------------------- Looping ----------------------------------------------------------");
                    }
                }

                var sb2 = new StringBuilder();
                Logger.Debug($"Starting lance instantiation");
                for (int x = 0; x < lance.Count; x++)
                {
                    sb2.Append($"{lance[x].Name} ");
                    __instance.AddMech(x, lance[x], true, true, false);
                }
                Logger.Debug($"Lance built: {sb2}");
            }
        }
    }

    internal class ModSettings
    {
        public List<string> StartingRonin = new List<string>();
        public int NumberProceduralPilots = 0;
        public int NumberRandomRonin = 4;

        public List<string> AssaultMechsPossible = new List<string>();
        public List<string> HeavyMechsPossible = new List<string>();
        public List<string> LightMechsPossible = new List<string>();
        public List<string> MediumMechsPossible = new List<string>();

        public int NumberAssaultMechs = 0;
        public int NumberHeavyMechs = 0;
        public int NumberLightMechs = 3;
        public int NumberMediumMechs = 1;

        public bool FullRandomMode = true;
        public float MinimumStartingWeight = 165;
        public float MaximumStartingWeight = 175;
        public int MinimumLanceSize = 4;
        public int MaximumLanceSize = 6;
        public float MaximumMechWeight = 100;
        public bool AllowDuplicateChassis = false;
        public bool AllowCustomMechs = false;
        public bool RemoveAncestralMech = false;

        public string ModDirectory = string.Empty;
        public bool Debug = false;
        public int Loops = 1;
    }

    public static class RngStart
    {
        internal static ModSettings Settings;

        public static void Init(string modDir, string modSettings)
        {
            var harmony = HarmonyInstance.Create("io.github.mpstark.RandomCampaignStart");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
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

