using Core.Steps;
using UnityEngine;

namespace Core.Battle
{

    [CreateAssetMenu(fileName = "Unit Config", menuName = "Battle Configs/Create Unit Config")]
    public class UnitConfig: ScriptableObject
    {
        [field: SerializeField] public int ID {  get; private set; }
        [field: SerializeField] public bool CanRepit { get; private set; } = true;
        [field: SerializeField] public string Name { get; private set; }
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
