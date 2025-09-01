using System;
using UnityEditor;
using UnityEngine;

namespace Core.Battle
{
    [CreateAssetMenu(fileName = "Level ", menuName = "Battle Configs/Create Level Config")]
    public class BattleConfig : ScriptableObject
    {
        [field: SerializeField] public Sprite Background { get; private set; }
        [field: SerializeField] public Wave[] Waves { get; private set; }

    }

    [Serializable]
    public class Wave
    {
        [field: SerializeField] public int HealValue { get; private set; } = 40;
        [field: SerializeField] public UnitConfig[] UnitConfigs { get; private set; }
    }
}