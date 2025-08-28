using Core.Battle;
using System.Linq;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Ultimate Attack Config", menuName = "Spells/Warrior/Create Ultimate Attack Config")]
    public class UltimateAttackConfig : BaseSpellConfig
    {
        [SerializeField] private int _damage;
        [SerializeField] private int _percentDamageOnHero;
        public override BaseSpell GetSpell() => new UltimateAttack(_damage, _percentDamageOnHero);
    }

    public class UltimateAttack : BaseSpell
    {
        private int _damage;
        private int _percentDamageOnHero;
        public UltimateAttack(int damage, int percentDamageOnHero)
        {
            _damage = damage;
            _percentDamageOnHero = percentDamageOnHero;
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
            battleController.HeroHealth.PercentDamage(_percentDamageOnHero);
        }
    }
}

