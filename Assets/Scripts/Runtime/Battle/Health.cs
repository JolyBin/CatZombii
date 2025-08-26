using System;
using UnityEngine;

namespace Core.Battle
{
    public class Health
    {
        public event Action OnDamaged;
        public event Action OnHealed;
        public event Action OnDied;
        public event Action<int, int> OnChanged; // (current, max)

        public int CurrentHP { get; private set; }
        public int MaxHP { get; private set; }
        public int AttackPriority { get; private set; }
        

        

        public Health(int maxHP, int attackPriority)
        {
            MaxHP = maxHP;
            AttackPriority = attackPriority;
            CurrentHP = Mathf.Max(1, maxHP);
        }

        public void SetMax(int value, bool refill = true)
        {
            MaxHP = Mathf.Max(1, value);
            if (refill) 
                CurrentHP = MaxHP;
            CurrentHP = Mathf.Clamp(CurrentHP, 0, MaxHP);
            OnChanged?.Invoke(CurrentHP, MaxHP);
        }

        public void TakeDamage(int amount)
        {
            if (CurrentHP <= 0) 
                return;
            if (amount <= 0) 
                return;

            CurrentHP = Mathf.Max(0, CurrentHP - amount);
            OnDamaged?.Invoke();
            OnChanged?.Invoke(CurrentHP, MaxHP);

            if (CurrentHP <= 0)
                Die();
        }

        public void PercentDamage(int pecent)
        {
            TakeDamage(CurrentHP * pecent / 100);
        }

        public void Heal(int amount)
        {
            if (amount <= 0)
                return;
            if (CurrentHP <= 0) 
                return;

            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
            OnHealed?.Invoke();
            OnChanged?.Invoke(CurrentHP, MaxHP);
        }

        private void Die()
        {
            OnDied?.Invoke();
            OnDamaged = null;
            OnHealed = null;
            OnDied = null;
            OnChanged = null;
        }
    }
}
