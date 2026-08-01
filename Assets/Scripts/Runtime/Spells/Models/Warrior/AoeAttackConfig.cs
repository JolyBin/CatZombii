using Core.Battle;
using Cysharp.Threading.Tasks;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Power AOE Attack Config", menuName = "Spells/Warrior/Create AOE Attack Config")]
    public class AoeAttackConfig : BaseSpellConfig
    {
        [SerializeField] private int _damage;
        // урон по КАЖДОМУ: показывать суммарный нельзя — он врал бы при одном враге
        public override int PreviewValue => _damage;
        public override BaseSpell GetSpell() => new AoeAttack(_damage);
    }

    public class AoeAttack: BaseSpell
    {
        private int _damage;
        public AoeAttack(int damage)
        {
            _damage = damage;
        }

        public override UniTask ApplySpell(BattleController battleController, CancellationToken token)
        {
            UnitRuntime[] enemyList = battleController.EnemySquad;
            if (enemyList.Length == 0)
                return UniTask.CompletedTask;
            foreach (var target in enemyList)
            {
                target.Health.TakeDamage(_damage);
            }

            return UniTask.CompletedTask;
        }
    }
}

