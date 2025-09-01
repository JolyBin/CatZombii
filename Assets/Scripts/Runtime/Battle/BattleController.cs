using Core.Spells;
using Core.Steps.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utility.Services.UI;

namespace Core.Battle
{
    public class BattleController
    {
        public event Action OnHeroDie;
        public event Action OnAllEnemyDie;
        public event Action<Health> OnAddEnemy;
        public event Action<Health> OnAddFriend;

        public Health HeroHealth { get; private set; }
        public UnitRuntime[] FriendlySquad => _friendlyList.ToArray();
        public UnitRuntime[] EnemySquad => _enemyList.ToArray();

        public UnitRuntime HeroTarget { get; private set; }

        private IUIService _uIService;
        private List<UnitRuntime> _friendlyList;
        private List<UnitRuntime> _enemyList;
        private UIBattleWindow _battleWindow;

        private BattleConfig _currentLevel;
        private Book _playerConfig;
        private TableController _tableController;
        private int _currentWaveIndex;

        public BattleController(IUIService uIService, BattleConfig levelConfig, Book playerConfig, TableController tableController)
        {
            _uIService = uIService;
            _battleWindow = _uIService.Get<UIBattleWindow>();
            _currentLevel = levelConfig;
            _playerConfig = playerConfig;
            _tableController = tableController;

            _friendlyList = new();
            _enemyList = new();
        }

        public void Init()
        {
            _currentWaveIndex = 0;

            HeroHealth = new Health(_playerConfig.HP, 0);
            _battleWindow.SetHero(_playerConfig);
            _battleWindow.SetHealth(HeroHealth.CurrentHP, HeroHealth.MaxHP);
            HeroHealth.OnChanged += _battleWindow.SetHealth;
            HeroHealth.OnDied += () =>
            {
                OnHeroDie?.Invoke();
                foreach (var item in _enemyList)
                {
                    item.TargetController.StopAttack();
                }
            };

            _tableController.OnSuccessfulMerge += (BaseSpell spell) => spell.ApplySpell(this);
            StartWave();
        }

        public void AddEnemy(UnitConfig unit)
        {
            if(_enemyList.Count > 3)
            {
                Debug.LogError("×ÎÒÀ ÍÅ ÒÎ, ÌÍÎÃÎ ÏÐÎÒÈÂÍÈÊÎÂ");
                return;
            }
            UnitRuntime unitRuntime = new UnitRuntime(unit);
            unitRuntime.UIUnit.OnSelectClickButton += () => SelectEnemyTarget(unitRuntime);
            UnitRuntime repit = _enemyList.Find(x => x.ID == unitRuntime.ID);
            if (repit != null && !unit.CanRepit)
            {
                _enemyList.Remove(repit);
                repit.Health.Die();
            }
            _enemyList.Add(unitRuntime);
            OnAddEnemy?.Invoke(unitRuntime.Health);

            if(unit.AttackCooldown > 0)
            {
                unitRuntime.TargetController.AddTarget(HeroHealth);
                foreach(var target in _friendlyList)
                {
                    unitRuntime.TargetController.AddTarget(target.Health);
                }

                OnAddFriend += unitRuntime.TargetController.AddTarget;
                unitRuntime.TargetController.StartAttack();
            }

            UIUnitPosition unitPosition = _battleWindow.SetEnemyPosition();
            unitPosition.SetUnit(unitRuntime.UIUnit.transform as RectTransform);

            unitRuntime.Health.OnDied += () =>
            {
                
                OnAddFriend -= unitRuntime.TargetController.AddTarget;
                _enemyList.Remove(unitRuntime);
                if (HeroTarget == unitRuntime)
                    SelectedLastTarget();
                unitRuntime.Dispose();
                unitPosition.SetFree();

                if (_enemyList.Count == 0)
                {
                    _currentWaveIndex++;
                    if(_currentLevel.Waves.Length == _currentWaveIndex)
                    {
                        OnAllEnemyDie?.Invoke();
                    }
                    else
                    {
                        StartWave();
                    }
                }
            };
        }

        public void AddFriend(UnitConfig unit)
        {
            UnitRuntime unitRuntime = new UnitRuntime(unit);
            UnitRuntime repit = _friendlyList.Find(x => x.ID == unitRuntime.ID);
            if (repit != null && !unit.CanRepit)
            {
                _friendlyList.Remove(repit);
                repit.Dispose();
            }
            _friendlyList.Add(unitRuntime);
            OnAddFriend?.Invoke(unitRuntime.Health);

            if (unit.AttackCooldown > 0)
            {
                foreach (var target in _enemyList)
                {
                    unitRuntime.TargetController.AddTarget(target.Health);
                }
                OnAddEnemy += unitRuntime.TargetController.AddTarget;
                unitRuntime.TargetController.StartAttack();
            }

            UIUnitPosition unitPosition = _battleWindow.SetFriendPosition();
            unitPosition.SetUnit(unitRuntime.UIUnit.transform as RectTransform);

            unitRuntime.Health.OnDied += () =>
            {
                unitPosition.SetFree();
                OnAddEnemy -= unitRuntime.TargetController.AddTarget;
                _friendlyList.Remove(unitRuntime);
                unitRuntime.Dispose();
            };
        }

        private void SelectEnemyTarget(UnitRuntime unitRuntime)
        {
            HeroTarget.UIUnit.Selected(false);
            HeroTarget = unitRuntime;
            HeroTarget.UIUnit.Selected(true);
        }

        private void SelectedLastTarget()
        {
            if (_enemyList.Count == 0)
                return;
            HeroTarget = _enemyList.Last();
            HeroTarget.UIUnit.Selected(true);
        }

        private void StartWave()
        {
            _battleWindow.SetWave(_currentWaveIndex + 1, _currentLevel.Waves.Length);
            foreach (UnitConfig unit in _currentLevel.Waves[_currentWaveIndex].UnitConfigs)
            {
                AddEnemy(unit);
            }

            SelectedLastTarget();
        }

        public void Exit()
        {
            foreach (var enemy in _enemyList)
            {
                enemy.Dispose();
            }
            foreach (var friend in _friendlyList)
            {
                friend.Dispose();
            }
            OnAllEnemyDie = null;
            OnHeroDie = null;
            OnAddEnemy = null;
            OnAddFriend = null;
            _enemyList = new();
            _friendlyList = new();
        }
    }
}
