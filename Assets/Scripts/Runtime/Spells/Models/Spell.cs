
using Core.Battle;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Spell", menuName = "Spells/Create Spell")]
    public class Spell : BaseSpellConfig
    {

        public override BaseSpell GetSpell()
        {
            throw new System.NotImplementedException();
        }
    }
}
