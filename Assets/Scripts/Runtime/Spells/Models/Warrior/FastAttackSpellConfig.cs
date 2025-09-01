using Core.Battle;
using Cysharp.Threading.Tasks;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Fast Attack Config", menuName = "Spells/Warrior/Create Fast Attack Config")]
    public class FastAttackSpellConfig : BaseSpellConfig
    {
        [field: SerializeField] public int Damage { get; private set; }
        [field: SerializeField] public int StunTimer { get; private set; }

        public override BaseSpell GetSpell() => new FastAttackSpell(Damage, StunTimer);
    }

    public class FastAttackSpell : BaseSpell
    {
        private int _damage;
        private int _stunTimer;

        public FastAttackSpell(int damage, int stunTimer)
        {
            _damage = damage;
            _stunTimer = stunTimer;
        }
        public override async void ApplySpell(BattleController battleController)
        {
            if (battleController.EnemySquad.Length == 0)
                return;
            UnitRuntime primaryTarget = battleController.HeroTarget;
            primaryTarget.Health.TakeDamage(_damage);
            primaryTarget.TargetController.StopAttack();
            await UniTask.Delay(_stunTimer * 1000);
            primaryTarget.TargetController.ContinueAttack();
        }
    }
}
