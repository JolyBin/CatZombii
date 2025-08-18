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
        public int AttackPriority { get; private set; }

        private int _maxHP = 100;
        

        

        public Health(int maxHP, int attackPriority)
        {
            _maxHP = maxHP;
            AttackPriority = attackPriority;
            CurrentHP = Mathf.Max(1, maxHP);
        }

        public void SetMax(int value, bool refill = true)
        {
            _maxHP = Mathf.Max(1, value);
            if (refill) 
                CurrentHP = _maxHP;
            CurrentHP = Mathf.Clamp(CurrentHP, 0, _maxHP);
            OnChanged?.Invoke(CurrentHP, _maxHP);
        }

        public void TakeDamage(int amount)
        {
            if (CurrentHP <= 0) 
                return;
            if (amount <= 0) 
                return;

            CurrentHP = Mathf.Max(0, CurrentHP - amount);
            OnDamaged?.Invoke();
            OnChanged?.Invoke(CurrentHP, _maxHP);

            if (CurrentHP <= 0) 
                OnDied?.Invoke();
        }

        public void Heal(int amount)
        {
            if (amount <= 0)
                return;
            if (CurrentHP <= 0) 
                return;

            CurrentHP = Mathf.Min(_maxHP, CurrentHP + amount);
            OnHealed?.Invoke();
            OnChanged?.Invoke(CurrentHP, _maxHP);
        }
    }
}
