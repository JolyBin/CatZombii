using Core.Steps;
using System;
using System.Collections.Generic;

namespace Core.Battle
{
    /// <summary>
    /// Боевой таймер одного юнита. ПОШАГОВЫЙ (docs/10 §0.2): собственного цикла ожидания
    /// больше нет, такты приходят снаружи из <see cref="WorldClock"/> через
    /// <see cref="BattleController"/>. Не двигается игрок — не двигается и этот таймер.
    ///
    /// Что это закрыло само собой:
    ///   — баг №7 (busy-loop стана): ждать нечего, стан просто считает такты;
    ///   — пауза боя под рекламу: мир и так стоит, пока игрок не ходит;
    ///   — часть отмены: цикла, который надо было бы отменять токеном, больше нет.
    /// </summary>
    public class TargetController : ITickable
    {
        public event Action OnAttack;
        public event Action<int, int> OnTimerChanged;

        /// <summary>
        /// Такт, в котором юнит ДЕЙСТВИТЕЛЬНО походил (не пауза, не стан).
        /// Нужен эффектам с собственным ритмом — например спавну помощников босса,
        /// чтобы и он считался в тактах, а не в миллисекундах.
        /// </summary>
        public event Action OnTactPassed;

        /// <summary>Тактов до удара. Целое число — его и показывает UI.</summary>
        public int CurrentTimer { get; private set; }
        public Health CurrentTarget { get; private set; }

        /// <summary>Тактов стана осталось. Пока больше нуля — юнит пропускает ход.</summary>
        public int StunTacts => _stunTacts;

        private int _startTimer;
        private List<Health> _targetsList;

        private bool _isLive;
        private bool _isPaused;
        private int _stunTacts;

        /// <param name="cooldownTacts">Кулдаун в ТАКТАХ мира, не в миллисекундах.</param>
        public TargetController(int cooldownTacts)
        {
            CurrentTimer = cooldownTacts;
            _startTimer = cooldownTacts;
            _targetsList = new();
        }

        public void SetNewTimerValue(int value)
        {
            _startTimer = value;
            // фаза босса ускоряет удары немедленно: держать длинный остаток от прошлой фазы
            // значит подарить игроку ход ровно там, где игра должна была ускориться
            CurrentTimer = Math.Clamp(CurrentTimer, 0, _startTimer);
            OnTimerChanged?.Invoke(CurrentTimer, _startTimer);
        }

        public void StopAttack()
        {
            _isPaused = true;
        }

        public void ContinueAttack()
        {
            _isPaused = false;
        }

        /// <summary>
        /// Стан в ТАКТАХ: юнит пропускает столько своих ходов. Раньше это был
        /// <c>StopAttack()</c> + <c>UniTask.Delay</c>, то есть стан таял по настенным часам
        /// и в пошаговом мире снимался бы, пока игрок просто думает.
        /// </summary>
        public void Stun(int tacts)
        {
            if (tacts <= 0)
                return;
            _stunTacts = Math.Max(_stunTacts, tacts);
            OnTimerChanged?.Invoke(CurrentTimer, _startTimer);
        }

        public void Dispose()
        {
            _isLive = false;
            _targetsList = new();
            CurrentTarget = null;
            OnAttack = null;
            OnTimerChanged = null;
            OnTactPassed = null;
        }

        public void StartAttack()
        {
            _isLive = true;
            _isPaused = false;
            _stunTacts = 0;
            OnTimerChanged?.Invoke(CurrentTimer, _startTimer);
        }

        public void AddTarget(Health target)
        {
            _targetsList.Add(target);
            target.OnDied += () =>
            {
                _targetsList.Remove(target);
            };
            SelectPrimaryTarget();

        }

        /// <summary>
        /// Один такт мира: снимаем такт стана либо приближаем удар ровно на единицу.
        /// Никаких дробных долей — «ходов до удара» это и есть шкала игрока.
        /// </summary>
        public void Tick()
        {
            if (!_isLive || _isPaused || _startTimer <= 0)
                return;

            if (_stunTacts > 0)
            {
                _stunTacts--;
                OnTimerChanged?.Invoke(CurrentTimer, _startTimer);
                return;
            }

            CurrentTimer = Math.Clamp(CurrentTimer - 1, 0, _startTimer);
            if (CurrentTimer <= 0)
            {
                CurrentTimer = _startTimer;
                AttackTarget();
            }

            OnTimerChanged?.Invoke(CurrentTimer, _startTimer);
            OnTactPassed?.Invoke();
        }

        private void SelectPrimaryTarget()
        {
            if(_targetsList.Count == 0)
            {
                return;
            }
            Health primaryTarget = _targetsList[0];
            int maxIndex = primaryTarget.AttackPriority;

            foreach (Health target in _targetsList)
            {
                if (target.AttackPriority > maxIndex)
                {
                    primaryTarget = target;
                    maxIndex = target.AttackPriority;
                }
            }

            CurrentTarget = primaryTarget;
        }

        private void AttackTarget()
        {
            if(CurrentTarget == null || CurrentTarget.CurrentHP <= 0)
            {
                SelectPrimaryTarget();
                if(CurrentTarget == null || CurrentTarget.CurrentHP <= 0)
                    return;
            }
            OnAttack?.Invoke();
        }
    }
}
