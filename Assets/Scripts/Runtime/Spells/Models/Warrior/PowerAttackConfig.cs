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
            if (battleController.EnemySquad.Length == 0)
                return;
            UnitRuntime primaryTarget = battleController.HeroTarget;
            primaryTarget.Health.TakeDamage(_damage);
        }
    }
}

