using UnityEngine;

namespace Core.Battle
{
    [CreateAssetMenu(fileName = "Single Attack", menuName = "Battle Configs/Create Single Attack Config")]
    public class SingleAttackConfig : BaseAttackConfig
    {
        [SerializeField] private int _damage;

        public override BaseAttack GetAttackClass() => new SingleAttack(_damage);
    }

    public class SingleAttack : BaseAttack
    {
        private int _damge;

        public SingleAttack(int damage)
        {
            _damge = damage;
        }

        public override void Attack(Health currentTarget, Health[] allTarget) => currentTarget.TakeDamage(_damge);
    }
}
