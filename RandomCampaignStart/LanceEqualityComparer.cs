using System.Collections.Generic;
using BattleTech;

namespace RandomCampaignStart
{
    public class LanceEqualityComparer: IEqualityComparer<MechDef>
    {
        public bool Equals(MechDef x, MechDef y)
        {
            return (x.Name == y.Name);
        }

        public int GetHashCode(MechDef obj)
        {
            return obj.Name.GetHashCode();
        }
    }
}