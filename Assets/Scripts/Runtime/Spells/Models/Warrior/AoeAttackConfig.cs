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

        public override async void ApplySpell(BattleController battleController)
        {
            UnitRuntime[] enemyList = battleController.EnemySquad;
            if (enemyList.Length == 0)
                return;
            foreach (var target in enemyList)
            {
                target.Health.TakeDamage(_damage);
            }
            
        }
    }
}

