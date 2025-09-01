using System;
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
        [field: SerializeField] public int AttackCooldown { get; private set; }
        [field: SerializeField] public int TargetPriority { get; private set; } = 0;

        [field: SerializeField] public BaseAttackConfig AttackConfig { get; private set; }
        [field: SerializeField] public UIUnit UnitPrefab { get; private set; }
    }

}