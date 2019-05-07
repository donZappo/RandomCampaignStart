using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BattleTech;
using BattleTech.UI;
using Harmony;
using static RandomCampaignStart.Logger;
using static RandomCampaignStart.RandomCampaignStart;

// ReSharper disable all InconsistentNaming

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
                cost += mechDef.Description.Cost *
                        RandomCampaignStart.ModSettings.MechPercentageStartingCost / 100;
            }

            __instance.AddFunds(-(int) cost, null, false);
        }
    }

    [HarmonyPatch(typeof(SimGameState), "_OnFirstPlayInit")]
    public class SimGameState_OnFirstPlayInitPatch
    {
        public static void Postfix(SimGameState __instance)
        {
            if (RandomCampaignStart.ModSettings.StartYear == -1) return;
            var date = new DateTime(RandomCampaignStart.ModSettings.StartYear, 1, 1);
            SetStartingDateTag(__instance, date);
            Traverse.Create(__instance).Property("CampaignStartDate").SetValue(date);
        }

        // credit to mpstark's Timeline mod
        private static void SetStartingDateTag(SimGameState simGame, DateTime startDate)
        {
            var startDateTag = "start_" + GetDayDateTag(startDate);
            Logger.LogDebug($"Setting the starting date tag: {startDateTag}");
            simGame.CompanyTags.Add(startDateTag);
        }

        private static string GetDayDateTag(DateTime date)
        {
            return $"timeline_{date:yyyy_MM_dd}";
        }
    }

    [HarmonyPatch(typeof(SimGameState), "FirstTimeInitializeDataFromDefs")]
    public static class SimGameState_FirstTimeInitializeDataFromDefs_Patch
    {
        private static MechDef AncestralMechDef;
        private static SimGameState Sim;

        public static void Postfix(SimGameState __instance)
        {
            LogDebug($"[START PILOT CREATION]");
            GeneratePilots(__instance);
            LogDebug($"[START LANCE CREATION {ModSettings.MinimumStartingWeight}-{ModSettings.MaximumStartingWeight} TONS, " +
                     $"{ModSettings.MinimumLanceSize}-{ModSettings.MaximumLanceSize} MECHS]");
            AncestralMechDef = new MechDef(__instance.DataManager.MechDefs.Get(__instance.ActiveMechs[0].Description.Id), __instance.GenerateSimGameUID());
            Sim = __instance;
            CreateLance();
        }

        private static void GeneratePilots(SimGameState __instance)
        {
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
                        LogDebug($"[ADDING RONIN {pilotDef.Description.Callsign}]");
                        // add directly to roster, don't want to get duplicate ronin from random ronin
                        if (pilotDef != null)
                        {
                            // convert pilot_sim_starter_medusa to PortraitPreset_medusa
                            // had to log out values to figure out what these presets were called
                            var portraitString = string.Join("", new[] {"PortraitPreset_", roninID.Split('_')[3]});
                            pilotDef.PortraitSettings = __instance.DataManager.PortraitSettings.Get(portraitString);
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
        }

        private static void CreateLance()
        {
            var originalLance = Sim.ActiveMechs;
            var lance = new List<MechDef>();
            var lanceWeight = 0;
            var mechDefs = Sim.DataManager.MechDefs.Select(kvp => kvp.Value).ToList();

            var mechQuery = mechDefs
                .Except(lance)
                .Where(mech => mech.Chassis.Tonnage <= ModSettings.MaximumMechWeight &&
                               mech.Chassis.Tonnage + lanceWeight <= ModSettings.MaximumStartingWeight &&
                               !mech.MechTags.Contains("BLACKLISTED") &&
                               !ModSettings.ExcludedMechs.Contains(mech.Chassis.VariantName));

            if (!ModSettings.AllowCustomMechs)
            {
                mechQuery = mechQuery.Where(mech => !mech.Name.ToUpper().Contains("CUSTOM"));
            }

            if (ModSettings.MechsAdhereToTimeline)
            {
                mechQuery = mechQuery
                    .Where(mech => mech.MinAppearanceDate <= Sim.CampaignStartDate);
            }

            HandleAncestral(lance, ref lanceWeight);

            while (lance.Count < ModSettings.MinimumLanceSize ||
                   lanceWeight < ModSettings.MinimumStartingWeight)
            {
                LogDebug($"[MECHS IN LIST {mechQuery.Count()}]");
                // this is the sanity clamp... anything unsolvable gets ignored
                if (mechQuery.Count() < ModSettings.MinimumLanceSize - lance.Count)
                {
                    LogDebug("[INSUFFICIENT MECHS - DEFAULT LANCE CREATION]");
                    Sim.ActiveMechs = originalLance;
                    return;
                }

                var mechDefString = mechQuery
                    .ElementAt(UnityEngine.Random.Range(0, mechQuery.Count())).Description.Id
                    .Replace("chassisdef", "mechdef");
                var mechDef = new MechDef(Sim.DataManager.MechDefs.Get(mechDefString), Sim.GenerateSimGameUID());
                LogDebug($"[GENERATED {mechDefString}]");

                if (mechDef.Chassis.Tonnage + lanceWeight <= ModSettings.MaximumStartingWeight &&
                    lance.Count < ModSettings.MaximumLanceSize &&
                    ModSettings.AllowDuplicateChassis |
                    !lance.Select(x => x.Name).Contains(mechDef.Name))
                {
                    lance.Add(mechDef);
                    lanceWeight += (int) mechDef.Chassis.Tonnage;
                    LogDebug($"[ADDING {mechDef.Description.Id}]");
                }
                else
                {
                    // it didn't fit but it's also the only option, so restart
                    if (mechQuery.Count() <= 1)
                    {
                        LogDebug("[BAD LANCE]");
                        lance.Clear();
                        HandleAncestral(lance, ref lanceWeight);
                        lanceWeight = 0;
                    }
                }

                LogDebug($"[TONS {lanceWeight} MECHS {lance.Count}]");
                if (lance.Count == ModSettings.MaximumLanceSize &&
                    lanceWeight < ModSettings.MinimumStartingWeight ||
                    lance.Count < ModSettings.MinimumLanceSize &&
                    lanceWeight >= ModSettings.MinimumStartingWeight)
                {
                    LogDebug("[BAD LANCE]");
                    lance.Clear();
                    HandleAncestral(lance, ref lanceWeight);
                    lanceWeight = 0;
                }
            }

            LogDebug("[COMPLETE: ADDING MECHS]");
            var sb = new StringBuilder();
            for (var x = 0; x < lance.Count; x++)
            {
                sb.AppendLine($"{lance[x].Chassis.VariantName} {lance[x].Name} ({((DateTime) lance[x].MinAppearanceDate).Year}) {lance[x].Chassis.Tonnage}T ({lance[x].Chassis.weightClass})");
                LogDebug($"Mech {x + 1} {lance[x].Name,-15} {lance[x].Chassis.VariantName,-10} {((DateTime) lance[x].MinAppearanceDate).Year,5} ({lance[x].Chassis.weightClass} {lance[x].Chassis.Tonnage}T)");
                Sim.AddMech(x, lance[x], true, true, false);
            }

            var tonnage = lance.GroupBy(x => x).Select(mech => mech.Key.Chassis.Tonnage).Sum();
            LogDebug($"[TONNAGE {tonnage}]");
            if (ModSettings.Reroll)
            {
                GenericPopupBuilder
                    .Create("This is your starting lance (" + tonnage + "T)", sb.ToString())
                    .AddButton("Proceed")
                    .AddButton("Re-roll", delegate { Reroll(); })
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

        private static void HandleAncestral(List<MechDef> lance, ref int lanceWeight)
        {
            if (ModSettings.RemoveAncestralMech)
            {
                RemoveAncestralMech(Sim, AncestralMechDef);
            }
            else if (ModSettings.IgnoreAncestralMech)
            {
                IgnoreAncestralMech(AncestralMechDef, lance);
            }
            else
            {
                AddAncestral(lance, ref lanceWeight);
            }
        }

        private static void AddAncestral(List<MechDef> lance, ref int lanceWeight)
        {
            LogDebug($"[ADD ANCESTRAL {AncestralMechDef.Name}]");
            lance.Add(AncestralMechDef);
            lanceWeight += (int) AncestralMechDef.Chassis.Tonnage;
        }

        private static void Reroll()
        {
            LogDebug("[RE-ROLL]");
            CreateLance();
        }

        private static void IgnoreAncestralMech(MechDef AncestralMechDef, List<MechDef> lance)
        {
            lance.Add(AncestralMechDef);
            ModSettings.MaximumLanceSize = ModSettings.MaximumLanceSize == 6
                ? 6
                : ModSettings.MaximumLanceSize + 1;
        }

        private static void RemoveAncestralMech(SimGameState __instance, MechDef AncestralMechDef)
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
    }
}