using Core.Battle;
using Cysharp.Threading.Tasks;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Ult Pets Spell Config", menuName = "Spells/Bio-Tamer/Create Ult Pets Spell Config")]
    public class UltPetConfig : BaseSpellConfig
    {
        [SerializeField] private CreatePetConfig[] _pets;
        public override BaseSpell GetSpell() => new UltPets(_pets);
    }

    public class UltPets: BaseSpell
    {
        private CreatePetConfig[] _pets;
        public UltPets(CreatePetConfig[] pets)
        {
            _pets = pets;
        }

        public override async UniTask ApplySpell(BattleController battleController, CancellationToken token)
        {
            foreach (var petConfig in _pets)
            {
                if (token.IsCancellationRequested)
                    return;
                await petConfig.GetSpell().ApplySpell(battleController, token);
            }
        }
    }
}

