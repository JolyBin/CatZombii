using Core.Steps;
using UnityEngine;
using Utility.Services.Localization;

namespace Core.Battle
{

    [CreateAssetMenu(fileName = "Unit Config", menuName = "Battle Configs/Create Unit Config")]
    public class UnitConfig: ScriptableObject
    {
        [field: SerializeField] public int ID {  get; private set; }
        [field: SerializeField] public bool CanRepit { get; private set; } = true;

        [Tooltip("КЛЮЧ строки, а не сама строка. Соглашение: unit.<имя_юнита>. " +
                 "ПУСТО — у юнита нет подписи вовсе (так у всех рядовых зомби), " +
                 "и это не то же самое, что пропавший ключ.")]
        [SerializeField] private string _nameKey;

        /// <summary>Ключ имени — для редакторной проверки и миграций, не для игры.</summary>
        public string NameKey => _nameKey;

        /// <summary>
        /// Подпись юнита в бою. Пустой ключ означает «без имени» — <c>UIUnit.SetName</c>
        /// в этом случае прячет надпись целиком, поэтому гонять пустоту через таблицу
        /// (и получать маркер <c>#unit.#</c> на экране) нельзя.
        /// </summary>
        public string Name => string.IsNullOrEmpty(_nameKey) ? string.Empty : Localization.Get(_nameKey);

        [field: SerializeField] public int HP { get; private set; } = 1;

        [Header("КАК ЧАСТО ВРАГ ХОДИТ — ЗАПОЛНЯТЬ ЗДЕСЬ")]
        [Tooltip("РАЗ ВО СКОЛЬКО ХОДОВ ИГРОКА враг бьёт. Ход игрока = перелив или варка.\n" +
                 "1 — каждый ход, 3 — раз в три хода, 4 — раз в четыре (docs/10 §13.7).\n" +
                 "Игра считает ТОЛЬКО в тактах: секунд и миллисекунд в бою больше нет.\n\n" +
                 "0 — значение НЕ ЗАДАНО: тогда оно выводится из легаси-миллисекунд ниже. " +
                 "Чтобы враг не атаковал вовсе — обнули ОБА поля.")]
        [SerializeField] private int _attackCooldownTacts;

        /// <summary>
        /// НАСЛЕДИЕ real-time: кулдаун в миллисекундах, как он записан в ассетах прототипа.
        /// Сам по себе игрой больше не используется — из него только выводится значение
        /// в тактах, и только пока <see cref="_attackCooldownTacts"/> не заполнен.
        /// Ноль в обоих полях по-прежнему означает «юнит не атакует вовсе».
        ///
        /// Поле оставлено ради существующих ассетов (<c>Assets/Resources/Battle/**</c>):
        /// у них заполнены миллисекунды, и стирать их — значит переверстать бестиарий
        /// до замера «переливов на схлопывание», то есть по несуществующим числам.
        /// </summary>
        // field: обязателен — заголовок и подсказка должны сесть на backing-поле
        // <AttackCooldown>k__BackingField, потому что инспектор рисует поля, а не свойства
        [field: Header("Наследие real-time — НЕ ЗАПОЛНЯТЬ (осталось от прототипа)")]
        [field: Tooltip("Миллисекунды старого real-time-боя. Работает ТОЛЬКО пока такты выше = 0, " +
                        "и переводится в такты делением на WorldClock.MILLISECONDS_PER_TACT " +
                        "(округление к ближайшему, минимум 1). Новые ассеты заполняют ТАКТЫ, а не это поле.")]
        [field: SerializeField] public int AttackCooldown { get; private set; }

        [field: SerializeField] public int TargetPriority { get; private set; } = 0;

        [field: SerializeField] public BaseUnitSpellConfig AttackConfig { get; private set; }
        [field: SerializeField] public UIUnit UnitPrefab { get; private set; }

        /// <summary>
        /// ЕДИНСТВЕННОЕ, ЧТО ЧИТАЕТ ИГРА. Такты авторятся напрямую; миллисекунды —
        /// фолбэк для ассетов, до которых ещё не дошли руки.
        ///
        /// Почему такты стали основными, а не остались override'ом. Пока первым и заполненным
        /// в инспекторе стояло поле в миллисекундах, геймдизайнер по построению вписывал
        /// туда миллисекунды — и получал не то, что задумал. Проект уже наступил на эту
        /// грабли один раз: конверсия кулдаунов в docs/10 §0.2 была посчитана по такту
        /// СХЛОПЫВАНИЯ вместо ПЕРЕЛИВА, и все враги слиплись в один такт. Ошибка была
        /// не в числе, а в том, что единица бралась не та, — а единицу задаёт то поле,
        /// которое видно первым.
        ///
        /// <see cref="_attackCooldownTacts"/> == 0 значит «не задано», а НЕ «ноль ходов»:
        /// ноль тактов означал бы бесплатное действие, то есть возврат к real-time.
        /// </summary>
        public int AttackCooldownTacts => WorldClock.TactsOrLegacyMilliseconds(_attackCooldownTacts, AttackCooldown);

        /// <summary>
        /// Откуда взялось действующее значение — для редакторной подсказки и отчётов.
        /// Игра этим не пользуется.
        /// </summary>
        public bool IsCooldownAuthoredInTacts => _attackCooldownTacts > 0;
    }

}
