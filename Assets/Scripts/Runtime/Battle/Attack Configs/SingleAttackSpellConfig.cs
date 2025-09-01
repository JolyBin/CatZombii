using UnityEngine;

namespace Core.Battle
{
    [CreateAssetMenu(fileName = "Single Attack", menuName = "Battle Configs/Create Single Attack Config")]
    public class SingleAttackSpellConfig : BaseUnitSpellConfig
    {
        [SerializeField] private int _damage;

        public override BaseUnitSpell GetUnitSpell() => new SingleAttackSpell(_damage);
    }

    public class SingleAttackSpell : BaseUnitSpell
    {
        private int _damge;

        public SingleAttackSpell(int damage)
        {
            _damge = damage;
        }

        public override void DisposeSpell()
        {
            
        }

        public override void InitSpell(UnitRuntime owner, BattleController battleController)
        {
            owner.TargetController.OnAttack += () => owner.TargetController.CurrentTarget.TakeDamage(_damge);
        }
    }
}
