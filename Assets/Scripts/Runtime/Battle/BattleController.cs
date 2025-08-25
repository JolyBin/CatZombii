using Core.Spells;
using Core.Steps.UI;
using System.Collections.Generic;
using UnityEngine;
using Utility.Services.UI;

namespace Core.Battle
{
    public class BattleController
    {
        public (Health health, TargetController targetController)[] FriendlySquad => _friendlyList.ToArray();
        public (Health health, TargetController targetController)[] EnemySquad => _enemyList.ToArray();

        private IUIService _uIService;
        private List<(Health health, TargetController targetController)> _friendlyList;
        private List<(Health health, TargetController targetController)> _enemyList;
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
            foreach (UnitConfig unit in _currentLevel.UnitConfigs)
            {
                UIUnitHealthBar uiUnitHealthBar = GameObject.Instantiate<UIUnitHealthBar>(unit.UnitPrefab);
                UIUnitTimerbar uiUnitTimerbar = uiUnitHealthBar.GetComponent<UIUnitTimerbar>();
                Health newHealth = new Health(unit.HP, unit.TargetPriority);
                TargetController targetController = new TargetController(unit.AttackCooldown);
                BaseAttack attack = unit.AttackConfig.GetAttackClass();
                _enemyList.Add((newHealth, targetController));

                uiUnitHealthBar.SetHealth(newHealth.CurrentHP, newHealth.MaxHP);
                uiUnitHealthBar.SetName(unit.Name);
                uiUnitTimerbar.SetTimer(0, unit.AttackCooldown);
                _battleWindow.SetEnemyPosition(uiUnitHealthBar.transform as RectTransform);

                newHealth.OnChanged += uiUnitHealthBar.SetHealth;
                targetController.OnTimerChanged += uiUnitTimerbar.SetTimer;
                targetController.OnAttack += attack.Attack;
            }

            Health heroHealth = new Health(_playerConfig.HP, 0);
            _friendlyList.Add((heroHealth, null));
            _battleWindow.SetHero(_playerConfig);
            _battleWindow.SetHealth(heroHealth.CurrentHP, heroHealth.MaxHP);
            heroHealth.OnChanged += _battleWindow.SetHealth;

            foreach(var enemy in _enemyList)
            {
                enemy.targetController.AddTarget(heroHealth);
                enemy.targetController.StartAttack();
            }
            _tableController.OnSuccessfulMerge += (BaseSpell spell) => spell.ApplySpell(_enemyList.ToArray());
        }

        public void Exit()
        {

        }
    }
}
