using Core.Battle;
using System.Linq;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Pet Spell Config", menuName = "Spells/Bio-Tamer/Create Pet Spell Config")]
    public class CreatePetConfig : BaseSpellConfig
    {
        [SerializeField] private UnitConfig _unitConfig;
        public override BaseSpell GetSpell() => new CreatePet(_unitConfig);
    }

    public class CreatePet: BaseSpell
    {
        private UnitConfig _unitConfig;
        public CreatePet(UnitConfig unitConfig)
        {
            _unitConfig = unitConfig;
        }

        public override async void ApplySpell(BattleController battleController)
        {
            battleController.AddFriend(_unitConfig);
        }
    }
}

