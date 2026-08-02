using Core.Battle;
using Core.Steps;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Regeniration Config", menuName = "Spells/Warrior/Create Regeniration Config")]
    public class RegenirationConfig : BaseSpellConfig
    {
        [SerializeField] private int _healthvalue = 10;

        [Tooltip("Сколько ТИКОВ лечения. 1 — мгновенное лечение без длительности (docs/10 §14.1).")]
        [SerializeField] private int _count = 3;

        // ТАКТЫ — ОСНОВНОЙ СПОСОБ АВТОРИНГА (правило и обоснование — в WorldClock).
        [Header("ПАУЗА МЕЖДУ ТИКАМИ — В ТАКТАХ МИРА. ЗАПОЛНЯТЬ ЗДЕСЬ")]
        [Tooltip("Сколько ХОДОВ ИГРОКА проходит между тиками лечения. 1 — каждый ход.\n" +
                 "0 — не задано, значение выведется из миллисекунд ниже.\n" +
                 "При _count = 1 не используется вовсе: лечение мгновенное.")]
        [SerializeField] private int _intervalTacts;

        /// <summary>
        /// Наследие real-time: пауза между тиками лечения в МИЛЛИСЕКУНДАХ, как в ассетах
        /// прототипа. Работает только пока такты выше равны нулю.
        /// </summary>
        [Header("Наследие real-time — НЕ ЗАПОЛНЯТЬ (осталось от прототипа)")]
        [Tooltip("Миллисекунды старого real-time-боя. Делятся на WorldClock.MILLISECONDS_PER_TACT. " +
                 "Новые ассеты заполняют ТАКТЫ, а не это поле.")]
        [SerializeField] private int _timer = 2000;

        /// <summary>Пауза между тиками в ТАКТАХ мира — сколько действий игрока между лечениями.</summary>
        public int IntervalTacts => WorldClock.TactsOrLegacyMilliseconds(_intervalTacts, _timer);

        /// <summary>Откуда взялось действующее значение — для редакторной подсказки. Игре не нужно.</summary>
        public bool IsIntervalAuthoredInTacts => _intervalTacts > 0;

        /// <summary>Сколько тиков лечения — для редакторной подсказки.</summary>
        public int HealCount => _count;

        // суммарное лечение: игроку важен итог, а не размер одного тика
        public override int PreviewValue => _healthvalue * _count;
        public override BaseSpell GetSpell() => new Regeniration(_healthvalue, IntervalTacts, _count);
    }

    /// <summary>
    /// Регенерация — единственный ДЛЯЩИЙСЯ эффект в игре, и в пошаговом мире он обязан
    /// длиться в ТАКТАХ. Раньше это был <c>UniTask.Delay</c>: лечение капало по настенным
    /// часам, то есть игрок, который просто перестал ходить, лечился бесплатно и в полной
    /// безопасности — враги-то стоят. Это сводило единственное лечение в игре к «сварил
    /// и подожди», без единого решения.
    ///
    /// Теперь тики приходят из <see cref="BattleController.OnTact"/>: пауза между ними
    /// оплачивается действиями игрока ровно так же, как всё остальное.
    /// </summary>
    public class Regeniration : BaseSpell
    {
        private readonly int _healthvalue;
        private readonly int _intervalTacts;
        private readonly int _count;

        private BattleController _battleController;
        private CancellationToken _token;
        private Action _onTactAction;
        private int _healsLeft;
        private int _tactsToNextHeal;

        public Regeniration(int healthvalue, int intervalTacts, int count)
        {
            _healthvalue = healthvalue;
            // ноль тактов между тиками означал бы «всё лечение мгновенно»,
            // то есть тихое превращение регенерации в обычный хил
            _intervalTacts = Math.Max(1, intervalTacts);
            _count = count;
        }

        /// <summary>
        /// Синхронная: ждать больше нечего, эффект просто подписывается на такты.
        /// Первый тик — НЕМЕДЛЕННО, в том же такте, что и варка. Игрок уже заплатил
        /// за варку ходом соседей, и если лечение начнёт капать только со следующего
        /// такта, то «варю лечение на трёх HP» проигрывается по построению.
        /// </summary>
        public override UniTask ApplySpell(BattleController battleController, CancellationToken token)
        {
            if (_count <= 0 || battleController.HeroHealth.CurrentHP <= 0)
                return UniTask.CompletedTask;

            _battleController = battleController;
            _token = token;
            _healsLeft = _count;

            Heal();
            if (_healsLeft <= 0)
                return UniTask.CompletedTask;

            _tactsToNextHeal = _intervalTacts;
            _onTactAction = OnTact;
            _battleController.OnTact += _onTactAction;
            return UniTask.CompletedTask;
        }

        private void OnTact()
        {
            if (_token.IsCancellationRequested || _battleController.HeroHealth.CurrentHP <= 0)
            {
                Unsubscribe();
                return;
            }

            _tactsToNextHeal--;
            if (_tactsToNextHeal > 0)
                return;

            _tactsToNextHeal = _intervalTacts;
            Heal();
            if (_healsLeft <= 0)
                Unsubscribe();
        }

        private void Heal()
        {
            _battleController.HeroHealth.Heal(_healthvalue);
            _healsLeft--;
        }

        private void Unsubscribe()
        {
            if (_onTactAction == null)
                return;
            _battleController.OnTact -= _onTactAction;
            _onTactAction = null;
        }
    }
}
