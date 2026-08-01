using UnityEngine;

namespace Core.Battle
{
    public class UnitRuntime
    {
        public int ID {  get; private set; }
        public Health Health { get; private set; }
        public TargetController TargetController { get; private set; }

        public UIUnit UIUnit { get; private set; }

        private BaseUnitSpell _unitSpell;


        public UnitRuntime(UnitConfig unit, BattleController battleController)
        {
            ID = unit.ID;
            Health = new Health(unit.HP, unit.TargetPriority);
            TargetController = new TargetController(unit.AttackCooldown);
            UIUnit = GameObject.Instantiate<UIUnit>(unit.UnitPrefab);
            UIUnit.Init();

            Health.OnChanged += UIUnit.SetHealth;
            UIUnit.SetHealth(Health.CurrentHP, Health.MaxHP);
            UIUnit.SetName(unit.Name);
            if (unit.AttackCooldown > 0)
            {

                _unitSpell = unit.AttackConfig.GetUnitSpell();
                UIUnit.SetActiveTimer(true);
                UIUnit.SetTimer(0, unit.AttackCooldown);

                TargetController.OnTimerChanged += UIUnit.SetTimer;
                _unitSpell.InitSpell(this, battleController);
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
            UIUnit.ClearAction();
            _unitSpell?.DisposeSpell();
            GameObject.Destroy(UIUnit.gameObject);
        }
    }
}
