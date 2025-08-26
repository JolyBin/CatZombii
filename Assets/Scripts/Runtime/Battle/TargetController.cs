using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Battle
{
    public class TargetController
    {
        public event Action<Health, Health[]> OnAttack;
        public event Action<int, int> OnTimerChanged;
        public int CurrentTimer { get; private set; }

        private int _timerStep;
        private int _startTimer;
        private List<Health> _targetsList;
        private Health _currentTarget;

        private bool _isLive;

        public TargetController(int timer)
        {
            CurrentTimer = timer * 1000;
            _startTimer = timer * 1000;
            _timerStep = 100;
            _targetsList = new();
        }

        public void StopAttack()
        {
            _timerStep = 0;
        }

        public void ContinueAttack()
        {
            _timerStep = 100;
        }

        public void Dispose()
        {
            _isLive = false;
            _targetsList = new();
            _currentTarget = null;
            OnAttack = null;
            OnTimerChanged = null;
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
                if(_currentTarget == null || _currentTarget.CurrentHP <= 0)
                    return;
            }
            OnAttack?.Invoke(_currentTarget, _targetsList.ToArray());
        }

        private async Task TimerAttack()
        {
            while (_isLive)
            {
                OnTimerChanged?.Invoke(CurrentTimer, _startTimer);
                CurrentTimer = Math.Clamp(CurrentTimer - _timerStep, 0, _startTimer);
                if(CurrentTimer <= 0)
                {
                    CurrentTimer = _startTimer;
                    AttackTarget();
                }
                await UniTask.Delay(_timerStep);
            }
        }
    }
}
