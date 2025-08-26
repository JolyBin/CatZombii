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

        public override void ApplySpell((Health health, TargetController targetController)[] allTargets, (Health health, TargetController targetController)[] allFriendly)
        {
            if (allTargets.Length == 0 || allFriendly.Length == 0)
                return;
            foreach (var target in allTargets)
            {
                target.health.TakeDamage(_damage);
            }
            allFriendly[0].health.PercentDamage(_percentDamageOnHero);
        }
    }
}

