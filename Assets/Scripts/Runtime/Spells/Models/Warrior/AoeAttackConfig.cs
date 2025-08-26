using Core.Battle;
using System.Linq;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Power AOE Attack Config", menuName = "Spells/Warrior/Create AOE Attack Config")]
    public class AoeAttackConfig : BaseSpellConfig
    {
        [SerializeField] private int _damage;
        public override BaseSpell GetSpell() => new AoeAttack(_damage);
    }

    public class AoeAttack: BaseSpell
    {
        private int _damage;
        public AoeAttack(int damage)
        {
            _damage = damage;
        }

        public override void ApplySpell((Health health, TargetController targetController)[] allTargets, (Health health, TargetController targetController)[] allFriendly)
        {
            if (allTargets.Length == 0)
                return;
            foreach (var target in allTargets)
            {
                target.health.TakeDamage(_damage);
            }
            
        }
    }
}

