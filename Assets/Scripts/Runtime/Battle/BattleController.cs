using Core.Spells;
using Core.Steps.UI;
using System.Collections.Generic;
using UnityEngine;
using Utility.Services.UI;

namespace Core.Battle
{
    public class BattleController
    {
        public Health[] FriendlySquad => _friendlyhealthList.ToArray();
        public Health[] EnemySquad => _enemyHealthList.ToArray();

        private IUIService _uIService;
        private List<Health> _friendlyhealthList;
        private List<TargetController> _friendlyTargetControllers;
        private List<Health> _enemyHealthList;
        private List<TargetController> _enemyTargetControllers;
        private UIBattleWindow _battleWindow;

        private BattleConfig _currentLevel;
        private Book _playerConfig;
        

        public BattleController(IUIService uIService, BattleConfig levelConfig, Book playerConfig)
        {
            _uIService = uIService;
            _battleWindow = _uIService.Get<UIBattleWindow>();
            _currentLevel = levelConfig;
            _playerConfig = playerConfig;

            _friendlyhealthList = new();
            _enemyHealthList = new();
            _enemyTargetControllers = new();
        }

        public void Init()
        {
            foreach (UnitConfig unit in _currentLevel.UnitConfigs)
            {
                UIUnitHealthBar uiUnitHealthBar = GameObject.Instantiate<UIUnitHealthBar>(unit.UnitPrefab);
                UIUnitTimerbar uiUnitTimerbar = uiUnitHealthBar.GetComponent<UIUnitTimerbar>();
                Health newHealth = new Health(unit.HP, unit.TargetPriority);
                TargetController targetController = new TargetController(unit.AttackCooldown);
                BaseAttack attack = unit.AttackConfig.GetAttackClass();
                _enemyHealthList.Add(newHealth);
                _enemyTargetControllers.Add(targetController);

                uiUnitHealthBar.SetHealth(newHealth.CurrentHP, newHealth.MaxHP);
                uiUnitHealthBar.SetName(unit.Name);
                uiUnitTimerbar.SetTimer(0, unit.AttackCooldown);
                _battleWindow.SetEnemyPosition(uiUnitHealthBar.transform as RectTransform);

                newHealth.OnChanged += uiUnitHealthBar.SetHealth;
                targetController.OnTimerChanged += uiUnitTimerbar.SetTimer;
                targetController.OnAttack += attack.Attack;
            }

            Health heroHealth = new Health(_playerConfig.HP, 0);
            _friendlyhealthList.Add(heroHealth);
            _battleWindow.SetHero(_playerConfig);
            _battleWindow.SetHealth(heroHealth.CurrentHP, heroHealth.MaxHP);
            heroHealth.OnChanged += _battleWindow.SetHealth;

            foreach(TargetController enemy in _enemyTargetControllers)
            {
                enemy.AddTarget(heroHealth);
                enemy.StartAttack();
            }
        }

        public void Exit()
        {

        }
    }
}
