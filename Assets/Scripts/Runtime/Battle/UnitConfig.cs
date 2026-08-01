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

        /// <summary>
        /// НАСЛЕДИЕ real-time: кулдаун в миллисекундах, как он записан в ассетах.
        /// Сам по себе игрой больше не используется — из него только выводится
        /// стартовое значение в тактах, см. <see cref="AttackCooldownTacts"/>.
        /// Ноль по-прежнему означает «юнит не атакует вовсе».
        /// </summary>
        [field: SerializeField] public int AttackCooldown { get; private set; }

        [Tooltip("Кулдаун в ТАКТАХ мира (переливах). 0 — вывести из миллисекунд через " +
                 "WorldClock.MILLISECONDS_PER_TACT. Ставь вручную, когда врага надо развести " +
                 "с остальными отдельно от общего множителя (docs/10 §0.2: «1 / 2 / 3 / 4 действия»).")]
        [SerializeField] private int _attackCooldownTacts;

        [field: SerializeField] public int TargetPriority { get; private set; } = 0;

        [field: SerializeField] public BaseUnitSpellConfig AttackConfig { get; private set; }
        [field: SerializeField] public UIUnit UnitPrefab { get; private set; }

        /// <summary>
        /// Кулдаун в тактах: ручной override, если он выставлен, иначе — перевод старого
        /// значения в миллисекундах единственным множителем конверсии. После замера
        /// «переливов на схлопывание» правится либо множитель (все враги разом),
        /// либо это поле у конкретного врага.
        /// </summary>
        public int AttackCooldownTacts => _attackCooldownTacts > 0
            ? _attackCooldownTacts
            : WorldClock.TactsFromMilliseconds(AttackCooldown);
    }

}
