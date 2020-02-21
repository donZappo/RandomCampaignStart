using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;

namespace RandomCampaignStart.Features
{
    public static class RandomizeSimGame
    {
        private static readonly Random RNG = new Random();


        public static void TryRandomize(SimGameState simGame)
        {
            // don't randomize if career is not with random mechs
            if (simGame.SimGameMode == SimGameState.SimGameType.CAREER)
                return;

            // don't randomize if campaign and we don't have that setting
            if (simGame.SimGameMode == SimGameState.SimGameType.KAMEA_CAMPAIGN
                && !Main.Settings.RandomizeStoryCampaign)
                return;

            if (Main.Settings.RandomizePilots)
            {
                var numPilots = Main.Settings.StartingRonin.Count + Main.Settings.NumberRandomRonin +
                    Main.Settings.NumberProceduralPilots;

                if (numPilots > 0)
                    RandomizePilots(simGame);
                else
                    Main.HBSLog.LogWarning("Tried to randomize pilots but settings had 0!");
            }

            if (simGame.SimGameMode == SimGameState.SimGameType.CAREER && Main.Settings.UseVanillaMechRandomizer)
                return;

            if (Main.Settings.RandomizeMechs)
            {
                var numMechs = Main.Settings.NumberLightMechs + Main.Settings.NumberMediumMechs +
                               Main.Settings.NumberHeavyMechs + Main.Settings.NumberAssaultMechs;

                if (numMechs > 0)
                    RandomizeMechs(simGame);
                else
                    Main.HBSLog.LogWarning("Tried to randomize mechs but settings had 0!");
            }
        }


        private static void RandomizeMechs(SimGameState simGame)
        {
            Main.HBSLog.Log("Randomizing mechs, removing old mechs");

            // clear the initial lance
            for (var i = 1; i < simGame.Constants.Story.StartingLance.Length + 1; i++)
                simGame.ActiveMechs.Remove(i);

            // remove ancestral mech if specified
            if (Main.Settings.RemoveAncestralMech)
            {
                Main.HBSLog.Log("\tRemoving ancestral mech");
                simGame.ActiveMechs.Remove(0);
            }

            // add the random mechs to mechIds
            var mechIds = new List<string>();
            mechIds.AddRange(GetRandomSubList(Main.Settings.AssaultMechsPossible, Main.Settings.NumberAssaultMechs));
            mechIds.AddRange(GetRandomSubList(Main.Settings.HeavyMechsPossible, Main.Settings.NumberHeavyMechs));
            mechIds.AddRange(GetRandomSubList(Main.Settings.MediumMechsPossible, Main.Settings.NumberMediumMechs));
            mechIds.AddRange(GetRandomSubList(Main.Settings.LightMechsPossible, Main.Settings.NumberLightMechs));

            // remove mechIDs that don't have a valid mechDef
            var numInvalid = mechIds.RemoveAll(id => !simGame.DataManager.MechDefs.Exists(id));
            if (numInvalid > 0)
                Main.HBSLog.LogWarning($"\tREMOVED {numInvalid} INVALID MECHS! Check mod.json for misspellings");

            // actually add the mechs to the game
            foreach (var mechID in mechIds)
            {
                var baySlot = simGame.GetFirstFreeMechBay();
                var mechDef = new MechDef(simGame.DataManager.MechDefs.Get(mechID), simGame.GenerateSimGameUID());

                if (baySlot >= 0)
                {
                    Main.HBSLog.Log($"\tAdding {mechID} to bay {baySlot}");
                    simGame.AddMech(baySlot, mechDef, true, true, false);
                }
                else
                {
                    Main.HBSLog.Log($"\tAdding {mechID} to storage, bays are full");
                    simGame.AddItemStat(mechDef.Chassis.Description.Id, mechDef.GetType(), false);
                }
            }
        }

        private static void RandomizePilots(SimGameState simGame)
        {
            Main.HBSLog.Log("Randomizing pilots, removing old pilots");

            // clear roster
            while (simGame.PilotRoster.Count > 0)
                simGame.PilotRoster.RemoveAt(0);

            // starting ronin that are always present
            if (Main.Settings.StartingRonin != null && Main.Settings.StartingRonin.Count > 0)
                foreach (var pilotID in Main.Settings.StartingRonin)
                {
                    if (!simGame.DataManager.PilotDefs.Exists(pilotID))
                    {
                        Main.HBSLog.LogWarning($"\tMISSING StartingRonin {pilotID}!");
                        continue;
                    }

                    var pilotDef = simGame.DataManager.PilotDefs.Get(pilotID);

                    if (Main.Settings.RerollRoninStats)
                        ReplacePilotStats(pilotDef,
                            simGame.PilotGenerator.GeneratePilots(1, Main.Settings.PilotPlanetDifficulty, 0, out _)[0]);

                    simGame.AddPilotToRoster(pilotDef, true);
                    Main.HBSLog.Log($"\tAdding StartingRonin {pilotDef.Description.Id}");
                }

            // random ronin
            if (Main.Settings.NumberRandomRonin > 0)
            {
                // make sure to remove the starting ronin list from the possible random pilots! yay linq
                var randomRonin =
                    GetRandomSubList(
                        simGame.RoninPilots.Where(x => !Main.Settings.StartingRonin.Contains(x.Description.Id))
                            .ToList(),
                        Main.Settings.NumberRandomRonin);
                foreach (var pilotDef in randomRonin)
                {
                    if (Main.Settings.RerollRoninStats)
                        ReplacePilotStats(pilotDef,
                            simGame.PilotGenerator.GeneratePilots(1, Main.Settings.PilotPlanetDifficulty, 0, out _)[0]);

                    simGame.AddPilotToRoster(pilotDef, true);
                    Main.HBSLog.Log($"\tAdding random Ronin {pilotDef.Description.Id}");
                }
            }

            // random procedural pilots
            if (Main.Settings.NumberProceduralPilots > 0)
            {
                var randomProcedural = simGame.PilotGenerator.GeneratePilots(Main.Settings.NumberProceduralPilots,
                    Main.Settings.PilotPlanetDifficulty, 0, out _);
                foreach (var pilotDef in randomProcedural)
                {
                    simGame.AddPilotToRoster(pilotDef, true);
                    Main.HBSLog.Log($"\tAdding random procedural pilot {pilotDef.Description.Id}");
                }
            }
        }


        private static void RNGShuffle<T>(IList<T> list)
        {
            // from https://stackoverflow.com/questions/273313/randomize-a-listt
            var n = list.Count;
            while (n > 1)
            {
                n--;
                var k = RNG.Next(n + 1);
                var value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        private static List<T> GetRandomSubList<T>(List<T> list, int numEntries)
        {
            var subList = new List<T>();

            if (list.Count <= 0 || numEntries <= 0)
                return subList;

            var randomizeMe = new List<T>(list);

            // add enough duplicates of the list to satisfy the number specified
            while (randomizeMe.Count < numEntries)
                randomizeMe.AddRange(list);

            RNGShuffle(randomizeMe);
            for (var i = 0; i < numEntries; i++)
                subList.Add(randomizeMe[i]);

            return subList;
        }

        private static void ReplacePilotStats(PilotDef pilotDef, PilotDef replacementStatPilotDef)
        {
            // set all stats to the subPilot stats
            pilotDef.AddBaseSkill(SkillType.Gunnery, replacementStatPilotDef.BaseGunnery - pilotDef.BaseGunnery);
            pilotDef.AddBaseSkill(SkillType.Piloting, replacementStatPilotDef.BasePiloting - pilotDef.BasePiloting);
            pilotDef.AddBaseSkill(SkillType.Guts, replacementStatPilotDef.BaseGuts - pilotDef.BaseGuts);
            pilotDef.AddBaseSkill(SkillType.Tactics, replacementStatPilotDef.BaseTactics - pilotDef.BaseTactics);

            pilotDef.ResetBonusStats();
            pilotDef.AddSkill(SkillType.Gunnery, replacementStatPilotDef.BonusGunnery);
            pilotDef.AddSkill(SkillType.Piloting, replacementStatPilotDef.BonusPiloting);
            pilotDef.AddSkill(SkillType.Guts, replacementStatPilotDef.BonusGuts);
            pilotDef.AddSkill(SkillType.Tactics, replacementStatPilotDef.BonusTactics);

            // set exp to replacementStatPilotDef
            pilotDef.SetSpentExperience(replacementStatPilotDef.ExperienceSpent);
            pilotDef.SetUnspentExperience(replacementStatPilotDef.ExperienceUnspent);

            // copy abilities
            pilotDef.abilityDefNames.Clear();
            pilotDef.abilityDefNames.AddRange(replacementStatPilotDef.abilityDefNames);
            pilotDef.AbilityDefs?.Clear();
            pilotDef.ForceRefreshAbilityDefs();
        }
    }
}
