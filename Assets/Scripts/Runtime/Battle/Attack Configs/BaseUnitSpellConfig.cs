using UnityEngine;

namespace Core.Battle
{
    public abstract class BaseUnitSpellConfig: ScriptableObject
    {
        public abstract BaseUnitSpell GetUnitSpell();
    }

    public abstract class BaseUnitSpell
    {
        public BaseUnitSpell() { }

        public abstract void InitSpell(UnitRuntime owner, BattleController battleController);

        public abstract void DisposeSpell();

    }
}
