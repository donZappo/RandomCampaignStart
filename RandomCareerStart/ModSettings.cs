using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RandomCampaignStart
{
    internal class ModSettings
    {
        public List<string> StartingRonin = new List<string>();

        public List<string> AssaultMechsPossible = new List<string>();
        public List<string> HeavyMechsPossible = new List<string>();
        public List<string> LightMechsPossible = new List<string>();
        public List<string> MediumMechsPossible = new List<string>();

        public int NumberAssaultMechs = 0;
        public int NumberHeavyMechs = 0;
        public int NumberLightMechs = 3;
        public int NumberMediumMechs = 1;

        public int NumberProceduralPilots = 0;
        public int NumberRandomRonin = 4;
        public int PilotPlanetDifficulty = 1;

        public bool RemoveAncestralMech = false;
        public bool RerollRoninStats = true;
        public bool RandomizeStoryCampaign = false;
        public bool UseVanillaMechRandomizer = false;
        public bool RandomizePilots = true;
        public bool RandomizeMechs = true;

        public static ModSettings ReadSettings(string json)
        {
            ModSettings settings;

            try
            {
                settings = JsonConvert.DeserializeObject<ModSettings>(json);
            }
            catch (Exception e)
            {
                Main.HBSLog.Log($"Reading settings failed: {e.Message}");
                settings = new ModSettings();
            }

            return settings;
        }
    }
}
