# Random Career Start
Gives random mechs and mechwarriors at the start of a career.
<br>

**SUMMARY**

Now start with random Mechs or Mercenaries at the start of your career! This allows for either fully random mechs where you can define the lance size, tonnage range, and a maximum tonnage per mech allowance. 

**NOTE**
If your settings cannot build a valid lance, then you will be given a lance of spiders! If this happens, take a close look at your settings and figure out what is wrong.

<br>

**SETTINGS**

* *Global toggles that effect both types of randomizers. FullRandomMode: true for complete randomization, false for Original 
Method Randomization.*

Setting | Description
--------|------------
MinimumStartingWeight: 160 | Specify weight range for lance
MaximumStartingWeight: 170 | 
NumberRandomRonin: 0 | How many random Ronin from the global pool in starting pool
NumberProceduralPilots: 3 | How many random rookie pilots in starting pool
MechPercentageStartingCost: 0 | Percentage cost of Mech Value to pay for your starting lance.
MechsAdhereToTimeline: true| Do you want only era appropriate starting mechs?
StartYear: 3025| If you are having era appropriate mechs, what is the latest appearance date?
MinimumLanceSize: 5 | Minimum size of starting lance.
MaximumLanceSize: 6 | Maximum size of starting lance.
MaximumMechWeight: 45 | Maximum individual mech tonnage in starting lance.
AllowDuplicateChassis: false | Are you allowed to have the same chassis in the starting lance?
AllowCustomMechs: false | Are you allowed to have custom chassis in the starting lance?
ExcludedMechs: [] | List of mechs by MechDef that cannot appear in a starting lance.


<br><br>



* *Specify which Ronin you want to have in your game to start. NumberRoninFromList will specify how many Ronin will be randomly selected from amongst this list.*

Setting | Description
--------|------------
NumberRoninFromList: 1 | How many ronin will be randomly selected from the following list
StartingRonin: | List of Ronin. Default is vanilla pilots
```
	"pilot_sim_starter_medusa",
	"pilot_sim_starter_behemoth",
	"pilot_sim_starter_dekker",
	"pilot_sim_starter_glitch"
```
