# RandomCampaignStart
Gives random mechs and mechwarriors at game start.

***SUMMARY***

Now start with random Mechs at the start of the campaign! This allows for either fully random mechs where you can define the lance size,
tonnage range, and a maximum tonnage per mech allowance. Also includes a tonnage range for the "traditional" mech randomizer where you
can input a list of mechs and specify amongst weight classes. 

***SETTINGS***

Global toggles that effect both types of randomizers. FullRandomMode: true for complete randomization, false for Original 
Method Randomization.

		"FullRandomMode" : true,
		
		"MinimumStartingWeight" : 160,
		
    	"MaximumStartingWeight" : 170,
		
		"NumberRandomRonin": 0,
		
    	"NumberProceduralPilots": 0,
	
   		 "StartingRonin": [],
		
		

Settings for Full Random Mode. Maximum Lance Size is 6.
    	"MinimumLanceSize" : 5,
		"MaximumLanceSize" : 6,
		"MaximumMechWeight" : 45,
		
Settings for Original Method. Use this method to input specific mechs to be randomized amongst. Also, this allows
precise control of mech weight classes.	
		"RemoveAncestralMech" : false,		
		
		"NumberAssaultMechs" : 0,
		"NumberHeavyMechs" : 0,
		"NumberLightMechs" : 3,
    	"NumberMediumMechs" : 1,
		"LightMechsPossible": [],
		"MediumMechsPossible": [],
		"HeavyMechsPossible": [],
		"AssaultMechsPossible": []
