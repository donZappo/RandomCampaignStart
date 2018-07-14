# FullRandomCampaignStart
Gives random mechs and mechwarriors at game start.

***SUMMARY***



Now start with random Mechs or Mercenaries at the start of the campaign! This allows for either fully random mechs where you can define the lance size, tonnage range, and a maximum tonnage per mech allowance. Also includes a tonnage range for the "traditional" mech randomizer where youcan input a list of mechs and specify amongst weight classes. 

***SETTINGS***

[Global toggles that effect both types of randomizers. FullRandomMode: true for complete randomization, false for Original 
Method Randomization.]

Setting | Description
--------|------------

* FullRandomMode: true		|		(true for Full Random Mode, false for Traditional Randome Mode)
* MinimumStartingWeight: 160		|	(Specify weight range for lance)
* MaximumStartingWeight: 170|
* NumberRandomRonin: 0			|	(How many random Ronin from the global pool in starting pool)
* NumberProceduralPilots: 0		|	(How many random rookie pilots in starting pool)


[Specify which Ronin you want to have in your game to start. NumberRoninFromList will specify how many Ronin will be randomly selected from amongst this list.]

NumberRoninFromList: 4				(How many ronin will be randomly selected from the following list)
StartingRonin:					(List of Ronin. Default is vanilla pilots)

	"pilot_sim_starter_medusa",
	"pilot_sim_starter_behemoth",
	"pilot_sim_starter_dekker",
	"pilot_sim_starter_glitch"



[Settings for Full Random Mode. Maximum Lance Size = 6.]

>MinimimumLanceSize: 5				(Specify minimum and maximum lance size to start)
>MaximumLanceSize: 6
>MaximumMechWeight: 45				(Heaviest mech that you can start with)
>AllowDuplicateChassis: false			(Can you get more than one of the same chassis type?)
>AllowCustomMechs: false			(In case you add custom mechs you can disable them to start)


[Settings for Original Method. Use this method to input specific mechs to be randomized amongst. Also, this allows precise control of mech weight classes.]

RemoveAncestralMech: false			(false - start with Ancestral BJ-1)
NumberAssaultMechs: 0				(How many of each weight class to start with)
NumberHeavyMechs: 0
NumberMediumMechs: 3
NumberLightMechs: 1
						(Only mechs in these lists will be used for lance creation)

"LightMechsPossible":
      "mechdef_locust_LCT-1M",
      "mechdef_locust_LCT-1S",
      "mechdef_locust_LCT-1V",
      "mechdef_locust_LCT-1E",
      "mechdef_locust_LCT-3V",
      "mechdef_commando_COM-1B",
      "mechdef_commando_COM-1C",
      "mechdef_commando_COM-1D",
      "mechdef_commando_COM-2D",
      "mechdef_commando_COM-3A",
      "mechdef_spider_SDR-5V",
      "mechdef_spider_SDR-5D",
      "mechdef_spider_SDR-5K",
      "mechdef_urbanmech_UM-R60",
      "mechdef_urbanmech_UM-R60C",
      "mechdef_urbanmech_UM-R60L",
      "mechdef_urbanmech_UM-R60X",
      "mechdef_firestarter_FS9-H",
      "mechdef_firestarter_FS9-A",
      "mechdef_firestarter_FS9-K",
      "mechdef_firestarter_FS9-M",
      "mechdef_jenner_JR7-D",
      "mechdef_jenner_JR7-A",
      "mechdef_jenner_JR7-F",
      "mechdef_panther_PNT-9R",
      "mechdef_panther_PNT-8Z",
      "mechdef_panther_PNT-9ALAG"
      
    "MediumMechsPossible":
      "mechdef_blackjack_BJ-1",
      "mechdef_blackjack_BJ-1DB",
      "mechdef_blackjack_BJ-1DC",
      "mechdef_blackjack_BJ-1X",
      "mechdef_cicada_CDA-3C",
      "mechdef_cicada_CDA-2B",
      "mechdef_cicada_CDA-2A",
      "mechdef_vindicator_VND-1R",
      "mechdef_vindicator_VND-1AA",
      "mechdef_vindicator_VND-1X"
    
    "HeavyMechsPossible":
      "mechdef_blackknight_BL-6-KNT",
      "mechdef_cataphract_CTF-1X",
      "mechdef_catapult_CPLT-C1",
      "mechdef_catapult_CPLT-K2",
      "mechdef_dragon_DRG-1N",
      "mechdef_grasshopper_GHR-5H",
      "mechdef_jagermech_JM6-A",
      "mechdef_jagermech_JM6-S",
      "mechdef_orion_ON1-K",
      "mechdef_orion_ON1-V",
      "mechdef_quickdraw_QKD-4G",
      "mechdef_quickdraw_QKD-5A",
      "mechdef_thunderbolt_TDR-5S",
      "mechdef_thunderbolt_TDR-5SE",
      "mechdef_thunderbolt_TDR-5SS"
    
    "AssaultMechsPossible":
      "mechdef_atlas_AS7-D",
      "mechdef_awesome_AWS-8Q",
      "mechdef_awesome_AWS-8T",
      "mechdef_banshee_BNC-3E",
      "mechdef_banshee_BNC-3M",
      "mechdef_battlemaster_BLR-1G",
      "mechdef_highlander_HGN-733",
      "mechdef_highlander_HGN-733P",
      "mechdef_kingcrab_KGC-0000",
      "mechdef_stalker_STK-3F",
      "mechdef_victor_VTR-9B",
      "mechdef_victor_VTR-9S",
      "mechdef_zeus_ZEU-6S"
    
	
