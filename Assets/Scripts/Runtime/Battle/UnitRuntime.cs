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
            // кулдаун берётся в ТАКТАХ мира: миллисекунды из ассета переводит UnitConfig
            int cooldownTacts = unit.AttackCooldownTacts;

            Health = new Health(unit.HP, unit.TargetPriority);
            TargetController = new TargetController(cooldownTacts);
            UIUnit = GameObject.Instantiate<UIUnit>(unit.UnitPrefab);
            UIUnit.Init();

            Health.OnChanged += UIUnit.SetHealth;
            UIUnit.SetHealth(Health.CurrentHP, Health.MaxHP);
            UIUnit.SetName(unit.Name);
            if (cooldownTacts > 0)
            {

                _unitSpell = unit.AttackConfig.GetUnitSpell();
                UIUnit.SetActiveTimer(true);
                // на старте показываем полный запас ходов, а не ноль: «через сколько ударит»
                UIUnit.SetTimer(cooldownTacts, cooldownTacts);

                TargetController.OnTimerChanged += UIUnit.SetTimer;
                _unitSpell.InitSpell(this, battleController);

                // замах — ПОСЛЕ удара, а не вместо него: урон уже нанесён этим же
                // событием выше по списку подписчиков. Отписка не нужна —
                // TargetController.Dispose() зануляет OnAttack целиком.
                TargetController.OnAttack += UIUnit.PlayAttack;
            }
            else
            {
                UIUnit.SetActiveTimer(false);
            }

        }

        /// <param name="playDeathAnimation">
        /// <c>true</c> — юнит умер в бою: даём ему доиграть смерть, объект убьёт себя сам
        /// по последнему кадру. <c>false</c> — партия закончилась и сцену разбирают,
        /// доигрывать нечего и негде: труп пережил бы окно боя и встретил следующую партию.
        /// </param>
        public void Dispose(bool playDeathAnimation = false)
        {
            Health.Dispose();
            TargetController.Dispose();
            UIUnit.ClearAction();
            _unitSpell?.DisposeSpell();

            if (playDeathAnimation && UIUnit.HasDeathFrames)
            {
                UIUnit.PlayDeath();
                return;
            }
            GameObject.Destroy(UIUnit.gameObject);
        }
    }
}
