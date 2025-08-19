using UnityEngine;

namespace Core.Battle
{
    [CreateAssetMenu(fileName = "Level ", menuName = "Battale Configs/Create Level Config")]
    public class BattleConfig : ScriptableObject
    {
        [field: SerializeField] public Sprite Background { get; private set; }
        [field: SerializeField] public UnitConfig[] UnitConfigs { get; private set; }

    }

}