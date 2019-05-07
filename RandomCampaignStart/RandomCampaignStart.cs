using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Harmony;
using Newtonsoft.Json;
using static RandomCampaignStart.Logger;

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
        private static readonly Random rng = new Random();

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
        public bool AllowCustomMechs = false;

        public float MechPercentageStartingCost = 0;
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