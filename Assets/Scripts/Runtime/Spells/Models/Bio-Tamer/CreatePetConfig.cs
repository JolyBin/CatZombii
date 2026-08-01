using Core.Battle;
using Cysharp.Threading.Tasks;
using System.Linq;
using System.Threading;
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

        public override UniTask ApplySpell(BattleController battleController, CancellationToken token)
        {
            battleController.AddFriend(_unitConfig);
            return UniTask.CompletedTask;
        }
    }
}

