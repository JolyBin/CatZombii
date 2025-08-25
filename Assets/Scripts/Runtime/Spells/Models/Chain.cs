using System.Collections.Generic;

namespace Core.Spells
{
    public class Chain
    {
        public int ID { get; private set; }
        public Dictionary<int, Chain> Chains { get; private set; }

        public BaseSpellConfig Spell { get; private set; }

        public Chain(int id)
        {
            ID = id;
            Chains = new Dictionary<int, Chain>();
        }

        public void SetSpell(BaseSpellConfig spell)
        { 
            Spell = spell; 
        }
    }
}
