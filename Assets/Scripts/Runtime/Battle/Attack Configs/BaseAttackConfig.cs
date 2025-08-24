using UnityEngine;

namespace Core.Battle
{
    public abstract class BaseAttackConfig: ScriptableObject
    {
        public abstract BaseAttack GetAttackClass();
    }

    public abstract class BaseAttack
    {
        public BaseAttack() { }

        public abstract void Attack(Health currentTarget, Health[] allTarget);
    }
}
