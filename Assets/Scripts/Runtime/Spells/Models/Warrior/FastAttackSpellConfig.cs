using Core.Battle;
using Cysharp.Threading.Tasks;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Fast Attack Config", menuName = "Spells/Warrior/Create Fast Attack Config")]
    public class FastAttackSpellConfig : BaseSpellConfig
    {
        [field: SerializeField] public int Damage { get; private set; }
        [field: SerializeField] public int StunTimer { get; private set; }

        public override int PreviewValue => Damage;

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
        public override async UniTask ApplySpell(BattleController battleController, CancellationToken token)
        {
            if (battleController.EnemySquad.Length == 0)
                return;
            UnitRuntime primaryTarget = battleController.HeroTarget;
            if (primaryTarget == null)
                return;
            primaryTarget.Health.TakeDamage(_damage);
            if (primaryTarget.Health.CurrentHP <= 0)
                return;
            primaryTarget.TargetController.StopAttack();

            bool isCanceled = await UniTask.Delay(_stunTimer * 1000, cancellationToken: token).SuppressCancellationThrow();
            if (isCanceled)
                return;
            if (primaryTarget.Health.CurrentHP <= 0)
                return;
            primaryTarget.TargetController.ContinueAttack();
        }
    }
}
