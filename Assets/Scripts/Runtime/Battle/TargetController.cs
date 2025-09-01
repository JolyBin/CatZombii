using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Battle
{
    public class TargetController
    {
        public event Action OnAttack;
        public event Action<int, int> OnTimerChanged;
        public int CurrentTimer { get; private set; }
        public Health CurrentTarget { get; private set; }

        private int _timerStep;
        private int _startTimer;
        private List<Health> _targetsList;

        private bool _isLive;

        public TargetController(int timer)
        {
            CurrentTimer = timer;
            _startTimer = timer;
            _timerStep = 100;
            _targetsList = new();
        }

        public void SetNewTimerValue(int value) => _startTimer = value; 

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
            CurrentTarget = null;
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

            CurrentTarget = primaryTarget;
        }

        private void AttackTarget()
        {
            if(CurrentTarget == null || CurrentTarget.CurrentHP <= 0)
            {
                SelectPrimaryTarget();
                if(CurrentTarget == null || CurrentTarget.CurrentHP <= 0)
                    return;
            }
            OnAttack?.Invoke();
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
