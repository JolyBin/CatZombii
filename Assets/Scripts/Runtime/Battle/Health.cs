using System;
using UnityEngine;

namespace Core.Battle
{
    public class Health
    {
        public event Action OnDamaged;
        public event Action OnHealed;
        public event Action OnDied;
        public event Action<int, int> OnChanged; // (current, max)

        /// <summary>
        /// Носитель встал. Отдельное событие, а не «OnChanged с ненулевым HP»:
        /// подписчику смерти надо знать именно про воскрешение, а не про то,
        /// что число изменилось.
        /// </summary>
        public event Action OnRevived;

        public int CurrentHP { get; private set; }
        public int MaxHP { get; private set; }
        public int AttackPriority { get; private set; }

        /// <summary>
        /// Умер ли носитель. Отдельный флаг, а не <c>CurrentHP &lt;= 0</c>: между
        /// «HP дошло до нуля» и «смерть обработана» есть промежуток — в нём как раз
        /// и крутится обработчик <see cref="OnDied"/>, который может добавить подписчиков
        /// или убить кого-то ещё. Флаг делает повторную смерть невозможной.
        /// </summary>
        public bool IsDead => _isDead;

        private bool _isDead;

        public Health(int maxHP, int attackPriority)
        {
            MaxHP = maxHP;
            AttackPriority = attackPriority;
            CurrentHP = Mathf.Max(1, maxHP);
        }

        public void SetMax(int value, bool refill = true)
        {
            MaxHP = Mathf.Max(1, value);
            if (refill)
                CurrentHP = MaxHP;
            CurrentHP = Mathf.Clamp(CurrentHP, 0, MaxHP);
            OnChanged?.Invoke(CurrentHP, MaxHP);
        }

        public void TakeDamage(int amount)
        {
            if (CurrentHP <= 0)
                return;
            if (amount <= 0)
                return;

            CurrentHP = Mathf.Max(0, CurrentHP - amount);
            OnDamaged?.Invoke();
            OnChanged?.Invoke(CurrentHP, MaxHP);

            if (CurrentHP <= 0)
                Die();
        }

        public void PercentDamage(int pecent)
        {
            TakeDamage(CurrentHP * pecent / 100);
        }

        public void Heal(int amount)
        {
            if (amount <= 0)
                return;
            if (CurrentHP <= 0)
                return;

            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
            OnHealed?.Invoke();
            OnChanged?.Invoke(CurrentHP, MaxHP);
        }

        /// <summary>
        /// Конец жизни ОБЪЕКТА, а не носителя. Зовёт владелец: <c>UnitRuntime.Dispose()</c>
        /// для юнита, <c>BattleController.Exit()</c> для героя. После этого здоровье
        /// уже ни на что не годно — воскрешать его нельзя, подписок больше нет.
        /// </summary>
        public void Dispose()
        {
            OnDamaged = null;
            OnHealed = null;
            OnDied = null;
            OnChanged = null;
            OnRevived = null;
        }

        /// <summary>
        /// СМЕРТЬ НОСИТЕЛЯ. Раньше здесь следом звался <see cref="Dispose"/> — это
        /// и был баг №10 (docs/06): события зануляются во время собственного вызова,
        /// и подписчик, добавленный ВНУТРИ обработчика <see cref="OnDied"/>, молча терялся.
        ///
        /// Теперь смерть не разбирает подписки. Разбирает их владелец — он и так это делал:
        /// у юнита обработчик <c>OnDied</c> в <c>BattleController</c> зовёт
        /// <c>UnitRuntime.Dispose()</c> → <see cref="Dispose"/> (то есть для юнитов
        /// не изменилось ничего), у героя подписки снимает <c>BattleController.Exit()</c>.
        /// Зато у героя UI остаётся привязанным — без этого <see cref="Revive"/> был бы
        /// невозможен в принципе: воскресший был бы жив в модели и мёртв на экране.
        ///
        /// От повторной смерти защищает <see cref="_isDead"/>, а не зануление события.
        /// Это важно: <c>BattleController.AddEnemy</c> добивает лишнего врага явным
        /// <c>Health.Die()</c>, и второй прогон обработчика засчитал бы конец волны дважды.
        /// </summary>
        public void Die()
        {
            if (_isDead)
                return;

            _isDead = true;
            CurrentHP = 0;
            OnDied?.Invoke();
        }

        /// <summary>
        /// ВОСКРЕШЕНИЕ. Нужно под rewarded-крючок «продолжить после поражения»
        /// (docs/09 пункт 8, docs/10 §10: «кот встаёт с 50% HP, волна сохраняется»).
        ///
        /// ПРО ПЕРЕПРИВЯЗКУ. Само по себе это здоровье своих подписчиков вернуть не может:
        /// те, кто отписался на смерти (враг вычеркнул цель из своего списка целей),
        /// отписались У СЕБЯ, а не здесь. Поэтому воскрешение — это ДВА шага,
        /// и второй обязателен:
        ///   1) этот метод поднимает носителя и кричит <see cref="OnRevived"/>;
        ///   2) владелец боя (<c>BattleController.ReviveHero</c>) заново раздаёт ссылки —
        ///      возвращает героя в списки целей врагов и обновляет полоску здоровья.
        /// Без второго шага получится ровно то, чего боялись: жив в модели, мёртв
        /// на экране, и по нему никто не бьёт.
        /// </summary>
        /// <param name="healthPoints">Сколько HP дать. 0 или меньше — полный запас.</param>
        /// <returns><c>false</c> — воскрешать было некого (носитель жив).</returns>
        public bool Revive(int healthPoints = 0)
        {
            if (!_isDead)
                return false;

            CurrentHP = healthPoints <= 0
                ? MaxHP
                : Mathf.Clamp(healthPoints, 1, MaxHP);
            _isDead = false;

            OnRevived?.Invoke();
            OnChanged?.Invoke(CurrentHP, MaxHP);
            return true;
        }

        /// <summary>Воскресить долей от максимума: 50 — половина. Меньше одного HP не бывает.</summary>
        public bool RevivePercent(int percentOfMax)
        {
            int healthPoints = Mathf.Max(1, MaxHP * percentOfMax / 100);
            return Revive(healthPoints);
        }
    }
}
