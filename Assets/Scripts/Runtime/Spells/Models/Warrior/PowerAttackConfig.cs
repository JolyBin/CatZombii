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

        public override async void ApplySpell(BattleController battleController)
        {
            UnitRuntime[] enemyList = battleController.EnemySquad;
            if (enemyList.Length == 0)
                return;
            UnitRuntime primaryTarget = enemyList.OrderBy(x => x.Health.AttackPriority).FirstOrDefault();
            primaryTarget.Health.TakeDamage(_damage);
        }
    }
}

