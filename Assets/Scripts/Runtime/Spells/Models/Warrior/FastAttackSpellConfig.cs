using Core.Battle;
using Core.Steps;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Fast Attack Config", menuName = "Spells/Warrior/Create Fast Attack Config")]
    public class FastAttackSpellConfig : BaseSpellConfig
    {
        [field: SerializeField] public int Damage { get; private set; }

        /// <summary>
        /// Наследие real-time: длительность стана в секундах, как записано в ассете.
        /// В пошаговом мире стан считается в ХОДАХ — иначе он таял бы, пока игрок думает,
        /// и снимался бы бесплатно. Перевод идёт единственным множителем конверсии
        /// (<see cref="WorldClock.MILLISECONDS_PER_TACT"/>), см. <see cref="StunTacts"/>.
        /// </summary>
        [field: SerializeField] public int StunTimer { get; private set; }

        /// <summary>Стан в тактах мира — сколько своих ходов цель пропустит.</summary>
        public int StunTacts => WorldClock.TactsFromSeconds(StunTimer);

        public override int PreviewValue => Damage;

        public override BaseSpell GetSpell() => new FastAttackSpell(Damage, StunTacts);
    }

    public class FastAttackSpell : BaseSpell
    {
        private int _damage;
        private int _stunTacts;

        public FastAttackSpell(int damage, int stunTacts)
        {
            _damage = damage;
            _stunTacts = stunTacts;
        }

        /// <summary>
        /// Стал синхронным: ждать больше нечего. Раньше стан держался парой
        /// StopAttack + UniTask.Delay + ContinueAttack, и это была та самая дыра
        /// из бага №7 — теперь цель просто пропускает N своих ходов.
        /// </summary>
        public override UniTask ApplySpell(BattleController battleController, CancellationToken token)
        {
            if (battleController.EnemySquad.Length == 0)
                return UniTask.CompletedTask;
            UnitRuntime primaryTarget = battleController.HeroTarget;
            if (primaryTarget == null)
                return UniTask.CompletedTask;
            primaryTarget.Health.TakeDamage(_damage);
            if (primaryTarget.Health.CurrentHP <= 0)
                return UniTask.CompletedTask;

            primaryTarget.TargetController.Stun(_stunTacts);
            return UniTask.CompletedTask;
        }
    }
}
