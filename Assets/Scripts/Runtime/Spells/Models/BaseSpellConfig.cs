using Core.Battle;
using UnityEngine;

namespace Core.Spells
{
    public abstract class BaseSpellConfig: ScriptableObject
    {
        [field: SerializeField] public string Name { get; private set; }
        public abstract BaseSpell GetSpell();
    }

    public abstract class BaseSpell
    {
        public virtual async void ApplySpell((Health health, TargetController targetController)[] allTargets, (Health health, TargetController targetController)[] allFriendly) { }
    }
}
