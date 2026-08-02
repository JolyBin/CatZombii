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

        // ТАКТЫ — ОСНОВНОЙ СПОСОБ АВТОРИНГА (правило и обоснование — в WorldClock).
        // Это поле особенно опасно было оставить в секундах: docs/10 §14.2 задаёт
        // «StunTimer: 1 / 2 / 3» ИМЕННО КАК ЧИСЛО ПРОПУЩЕННЫХ ХОДОВ («враг пропускает
        // ход»), а в секундах это же число совпадёт с задуманным только по случайности —
        // ровно пока MILLISECONDS_PER_TACT равен 1000. После замера «переливов
        // на схлопывание» множитель изменится, и «стан 3» тихо станет станом на 1 ход.
        [Header("СТАН — В ТАКТАХ МИРА. ЗАПОЛНЯТЬ ЗДЕСЬ")]
        [Tooltip("СКОЛЬКО СВОИХ ХОДОВ цель пропустит. 1 — пропускает один удар, 3 — три.\n" +
                 "0 — не задано, значение выведется из секунд ниже.")]
        [SerializeField] private int _stunTacts;

        /// <summary>
        /// Наследие real-time: длительность стана в СЕКУНДАХ, как записано в ассетах
        /// прототипа. Работает только пока такты выше равны нулю.
        /// </summary>
        // field: обязателен — заголовок должен сесть на backing-поле, инспектор рисует поля
        [field: Header("Наследие real-time — НЕ ЗАПОЛНЯТЬ (осталось от прототипа)")]
        [field: Tooltip("Секунды старого real-time-боя. Переводятся в такты как секунды*1000 / " +
                        "WorldClock.MILLISECONDS_PER_TACT. Новые ассеты заполняют ТАКТЫ, а не это поле.")]
        [field: SerializeField] public int StunTimer { get; private set; }

        /// <summary>Стан в тактах мира — сколько своих ходов цель пропустит. Это и читает игра.</summary>
        public int StunTacts => WorldClock.TactsOrLegacySeconds(_stunTacts, StunTimer);

        /// <summary>Откуда взялось действующее значение — для редакторной подсказки. Игре не нужно.</summary>
        public bool IsStunAuthoredInTacts => _stunTacts > 0;

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
