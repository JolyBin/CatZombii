using Core.Spells;
using Core.Steps.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility.Services.UI;

namespace Core.Battle
{
    public class UnitRuntime
    {
        public Health Health { get; private set; }
        public TargetController TargetController { get; private set; }

        public UIUnit UIUnit { get; private set; }

        public UnitRuntime(UnitConfig unit)
        {
            Health = new Health(unit.HP, unit.TargetPriority);
            TargetController = new TargetController(unit.AttackCooldown);
            UIUnit = GameObject.Instantiate<UIUnit>(unit.UnitPrefab);

            Health.OnChanged += UIUnit.SetHealth;
            UIUnit.SetHealth(Health.CurrentHP, Health.MaxHP);
            UIUnit.SetName(unit.Name);
            if (unit.AttackCooldown > 0)
            {

                BaseAttack attack = unit.AttackConfig.GetAttackClass();
                UIUnit.SetActiveTimer(true);
                UIUnit.SetTimer(0, unit.AttackCooldown);

                TargetController.OnTimerChanged += UIUnit.SetTimer;
                TargetController.OnAttack += attack.Attack;
            }
            else
            {
                UIUnit.SetActiveTimer(false);
            }

        }

        public void Dispose()
        {
            Health.Dispose();
            TargetController.Dispose(); 
            GameObject.Destroy(UIUnit.gameObject);
        }
    }

    public class BattleController
    {
        public event Action OnHeroDie;
        public event Action OnAllEnemyDie;

        public Health HeroHealth { get; private set; }
        public UnitRuntime[] FriendlySquad => _friendlyList.ToArray();
        public UnitRuntime[] EnemySquad => _enemyList.ToArray();

        private IUIService _uIService;
        private List<UnitRuntime> _friendlyList;
        private List<UnitRuntime> _enemyList;
        private UIBattleWindow _battleWindow;

        private BattleConfig _currentLevel;
        private Book _playerConfig;
        private TableController _tableController;
        

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

            foreach (UnitConfig unit in _currentLevel.UnitConfigs)
            {
                AddEnemy(unit);

            }


            _tableController.OnSuccessfulMerge += (BaseSpell spell) => spell.ApplySpell(this);
        }

        public void AddEnemy(UnitConfig unit)
        {
            UnitRuntime unitRuntime = new UnitRuntime(unit);

            _enemyList.Add(unitRuntime);
            _battleWindow.SetEnemyPosition(unitRuntime.UIUnit.transform as RectTransform);

            if(unit.AttackCooldown > 0)
            {
                unitRuntime.TargetController.AddTarget(HeroHealth);
                foreach(var target in _friendlyList)
                {
                    unitRuntime.TargetController.AddTarget(target.Health);
                }
                unitRuntime.TargetController.StartAttack();
            }

            unitRuntime.Health.OnDied += () =>
            {
                _enemyList.Remove(unitRuntime);
                unitRuntime.Dispose();
                if (_enemyList.Count == 0)
                    OnAllEnemyDie?.Invoke();
            };
        }

        public void AddFriend(UnitConfig unit)
        {
            UnitRuntime unitRuntime = new UnitRuntime(unit);

            _friendlyList.Add(unitRuntime);
            //_battleWindow.SetEnemyPosition(unitRuntime.UIUnit.transform as RectTransform);

            if (unit.AttackCooldown > 0)
            {
                foreach (var target in _enemyList)
                {
                    unitRuntime.TargetController.AddTarget(target.Health);
                }
                unitRuntime.TargetController.StartAttack();
            }

            unitRuntime.Health.OnDied += () =>
            {
                _friendlyList.Remove(unitRuntime);
                unitRuntime.Dispose();
            };
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
            _enemyList = new();
            _friendlyList = new();
        }
    }
}
