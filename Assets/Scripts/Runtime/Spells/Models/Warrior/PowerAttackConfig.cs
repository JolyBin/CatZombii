using Core.Battle;
using Cysharp.Threading.Tasks;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Power Attack Config", menuName = "Spells/Warrior/Create Power Attack Config")]
    public class PowerAttackConfig : BaseSpellConfig
    {
        [SerializeField] private int _damage;
        public override int PreviewValue => _damage;
        public override BaseSpell GetSpell() => new PowerAttack(_damage);
    }

    public class PowerAttack: BaseSpell
    {
        private int _damage;
        public PowerAttack(int damage)
        {
            _damage = damage;
        }

        public override UniTask ApplySpell(BattleController battleController, CancellationToken token)
        {
            if (battleController.EnemySquad.Length == 0)
                return UniTask.CompletedTask;
            UnitRuntime primaryTarget = battleController.HeroTarget;
            if (primaryTarget == null)
                return UniTask.CompletedTask;
            primaryTarget.Health.TakeDamage(_damage);
            return UniTask.CompletedTask;
        }
    }
}

