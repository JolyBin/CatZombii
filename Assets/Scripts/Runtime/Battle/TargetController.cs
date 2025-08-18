using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Battle
{
    public class TargetController
    {
        public event Action OnAllTargetsDie;
        public event Action OnAttack;
        public int CurrentTimer { get; private set; }

        private int _damage;
        private int _timerStep;
        private int _startTimer;
        private List<Health> _targetsList;
        private Health _currentTarget;

        private bool _isLive;

        public TargetController(int dmage, int timer)
        {
            _damage = dmage;
            CurrentTimer = timer;
            _startTimer = timer;
            _timerStep = 100;
            _targetsList = new();
        }

        public void StopAttack()
        {
            _timerStep = 0;
        }

        public void StartAttac()
        {
            _timerStep = 100;
        }

        public void Die()
        {
            _isLive = false;
            OnAllTargetsDie = null;
            _targetsList = new();
            _currentTarget = null;
            OnAttack = null;
        }

        public void StartAttack()
        {
            _isLive = true;
            TimerAttack();
        }

        public void AddTarget(Health target)
        {
            _targetsList.Add(target);
            target.OnDied += () => 
            {
                _targetsList.Remove(target);
            };
            SelectPrimaryTarget();

        }

        private void SelectPrimaryTarget()
        {
            if(_targetsList.Count == 0)
            {
                OnAllTargetsDie?.Invoke();
                return;
            }
            Health primaryTarget = _targetsList[0];
            int maxIndex = primaryTarget.AttackPriority;

            foreach (Health target in _targetsList)
            {
                if (target.AttackPriority > maxIndex)
                {
                    primaryTarget = target;
                    maxIndex = target.AttackPriority;
                }
            }

            _currentTarget = primaryTarget;
        }

        private void AttackTarget()
        {
            if(_currentTarget == null || _currentTarget.CurrentHP <= 0)
            {
                SelectPrimaryTarget();
                return;
            }
            _currentTarget.TakeDamage(_damage);
        }

        private async Task TimerAttack()
        {
            while (_isLive)
            {
                CurrentTimer = Math.Clamp(CurrentTimer - _timerStep, 0, _startTimer);
                if(CurrentTimer <= 0)
                {
                    CurrentTimer = _startTimer;
                    AttackTarget();
                    OnAttack?.Invoke();
                }
                await UniTask.Delay(_timerStep);
            }
        }
    }
}
