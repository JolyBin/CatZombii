using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Core.Battle
{
    public class TargetController
    {
        public event Action OnAttack;
        public event Action<int, int> OnTimerChanged;
        public int CurrentTimer { get; private set; }
        public Health CurrentTarget { get; private set; }

        private const int TIMER_STEP = 100;

        private int _startTimer;
        private List<Health> _targetsList;

        private bool _isLive;
        private bool _isPaused;

        public TargetController(int timer)
        {
            CurrentTimer = timer;
            _startTimer = timer;
            _targetsList = new();
        }

        public void SetNewTimerValue(int value) => _startTimer = value;

        public void StopAttack()
        {
            _isPaused = true;
        }

        public void ContinueAttack()
        {
            _isPaused = false;
        }

        public void Dispose()
        {
            _isLive = false;
            _targetsList = new();
            CurrentTarget = null;
            OnAttack = null;
            OnTimerChanged = null;
        }

        public void StartAttack(CancellationToken token)
        {
            _isLive = true;
            _isPaused = false;
            TimerAttack(token).Forget();
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

        private async UniTaskVoid TimerAttack(CancellationToken token)
        {
            try
            {
                while (_isLive && !token.IsCancellationRequested)
                {
                    OnTimerChanged?.Invoke(CurrentTimer, _startTimer);
                    if (!_isPaused)
                    {
                        CurrentTimer = Math.Clamp(CurrentTimer - TIMER_STEP, 0, _startTimer);
                        if (CurrentTimer <= 0)
                        {
                            CurrentTimer = _startTimer;
                            AttackTarget();
                        }
                    }
                    bool isCanceled = await UniTask.Delay(TIMER_STEP, cancellationToken: token).SuppressCancellationThrow();
                    if (isCanceled)
                        return;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
