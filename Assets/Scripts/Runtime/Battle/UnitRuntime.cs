using UnityEngine;

namespace Core.Battle
{
    public class UnitRuntime
    {
        public int ID {  get; private set; }
        public Health Health { get; private set; }
        public TargetController TargetController { get; private set; }

        public UIUnit UIUnit { get; private set; }

        public UnitRuntime(UnitConfig unit)
        {
            ID = unit.ID;
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
}
