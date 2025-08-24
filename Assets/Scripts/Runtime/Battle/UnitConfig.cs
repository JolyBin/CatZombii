using System;
using UnityEngine;

namespace Core.Battle
{
    [Serializable]
    public class UnitConfig
    {
        [field: SerializeField] public string Name { get; private set; }
        [field: SerializeField] public int HP { get; private set; } = 1;
        [field: SerializeField] public int AttackCooldown { get; private set; }
        [field: SerializeField] public int TargetPriority { get; private set; } = 0;

        [field: SerializeField] public BaseAttackConfig AttackConfig { get; private set; }
        [field: SerializeField] public UIUnitHealthBar UnitPrefab { get; private set; }
    }

}