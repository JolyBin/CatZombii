using Core.Battle;
using System.Linq;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Power Attack Config", menuName = "Spells/Warrior/Create Power Attack Config")]
    public class PowerAttackConfig : BaseSpellConfig
    {
        [SerializeField] private int _damage;
        public override BaseSpell GetSpell() => new PowerAttack(_damage);
    }

    public class PowerAttack: BaseSpell
    {
        private int _damage;
        public PowerAttack(int damage)
        {
            _damage = damage;
        }

        public override void ApplySpell((Health health, TargetController targetController)[] allTargets, (Health health, TargetController targetController)[] allFriendly)
        {
            if (allTargets.Length == 0)
                return;
            (Health health, TargetController targetController) primaryTarget = allTargets.OrderBy(x => x.health.AttackPriority).FirstOrDefault();
            primaryTarget.health.TakeDamage(_damage);
        }
    }
}

