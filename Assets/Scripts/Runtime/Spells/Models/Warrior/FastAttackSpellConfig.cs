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
        public override async void ApplySpell((Health health, TargetController targetController)[] allTargets, (Health health, TargetController targetController)[] allFriedly)
        {
            if (allTargets.Length == 0)
                return;
            (Health health, TargetController targetController) primaryTarget = allTargets.OrderBy(x => x.health.AttackPriority).FirstOrDefault();
            primaryTarget.health.TakeDamage(_damage);
            primaryTarget.targetController.StopAttack();
            await UniTask.Delay(_stunTimer * 1000);
            primaryTarget.targetController.ContinueAttack();
        }
    }
}
