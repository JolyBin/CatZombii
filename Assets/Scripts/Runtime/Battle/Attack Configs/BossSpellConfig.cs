using Core.Steps;
using UnityEngine;

namespace Core.Battle
{
    [CreateAssetMenu(fileName = "Boss Spell Config", menuName = "Battle Configs/Create Boss Spell Config")]
    public class BossSpellConfig : BaseUnitSpellConfig
    {
        [SerializeField] private int _damage;
        [SerializeField] private UnitConfig _simbpleZombieConfig;
        [SerializeField] private UnitConfig _ratZombieConfig;

        // ТАКТЫ — ОСНОВНОЙ СПОСОБ АВТОРИНГА (правило и его обоснование — в WorldClock).
        // docs/10 §13.7: «босс — раз в 2 хода, меняется по фазам». Фаза 1 задаётся
        // кулдауном самого юнита (UnitConfig), фазы 2 и 3 — здесь.
        [Header("ФАЗЫ БОССА — В ТАКТАХ МИРА. ЗАПОЛНЯТЬ ЗДЕСЬ")]
        [Tooltip("Раз во сколько ходов игрока бьёт босс на ФАЗЕ 2 (ниже 70% HP). 0 — не задано, взять из миллисекунд ниже.")]
        [SerializeField] private int _twoPhaseAttackCooldownTacts;
        [Tooltip("Раз во сколько ходов игрока бьёт босс на ФАЗЕ 3 (ниже 30% HP). 0 — не задано, взять из миллисекунд ниже.")]
        [SerializeField] private int _threePhaseAttackCooldownTacts;
        [Tooltip("Раз во сколько СВОИХ ходов босс призывает волну помощников (фаза 3). " +
                 "Считается по ходам босса, а не игрока: стан морозит и призыв тоже. " +
                 "0 — не задано, взять из миллисекунд ниже.")]
        [SerializeField] private int _spawnCooldownTacts;

        // Наследие real-time: миллисекунды из ассета прототипа. Работают ТОЛЬКО пока
        // соответствующее поле в тактах равно нулю. При MILLISECONDS_PER_TACT = 1000 дают
        // 15000 -> 15 ходов на волну помощников, 3500 -> 4 и 2500 -> 3 хода на удар.
        [Header("Наследие real-time — НЕ ЗАПОЛНЯТЬ (осталось от прототипа)")]
        [SerializeField] private int _spawnColldawn = 10000;
        [SerializeField] private int _twoPhaseAttackCooldown = 3500;
        [SerializeField] private int _threePhaseAttackCooldown = 2500;

        /// <summary>Действующие такты фазы 2 — то, что реально уедет в бой.</summary>
        public int TwoPhaseAttackCooldownTacts
            => WorldClock.TactsOrLegacyMilliseconds(_twoPhaseAttackCooldownTacts, _twoPhaseAttackCooldown);

        /// <summary>Действующие такты фазы 3.</summary>
        public int ThreePhaseAttackCooldownTacts
            => WorldClock.TactsOrLegacyMilliseconds(_threePhaseAttackCooldownTacts, _threePhaseAttackCooldown);

        /// <summary>Действующие такты между волнами помощников.</summary>
        public int SpawnCooldownTacts
            => WorldClock.TactsOrLegacyMilliseconds(_spawnCooldownTacts, _spawnColldawn);

        public override BaseUnitSpell GetUnitSpell() => new BossSpell(_damage, _simbpleZombieConfig, _ratZombieConfig,
                                                                      SpawnCooldownTacts,
                                                                      TwoPhaseAttackCooldownTacts,
                                                                      ThreePhaseAttackCooldownTacts);
    }

    /// <summary>
    /// Босс. ПОШАГОВЫЙ целиком: и удары, и призыв помощников считаются в тактах мира.
    /// Прежний призыв крутился на <c>UniTask.Delay</c>, то есть в пошаговой игре
    /// босс продолжал бы наводнять экран, пока игрок стоит и думает, — прямое нарушение
    /// правила «мир двигается только вместе с игроком» (docs/10 §0.2).
    /// Такты приходят из <see cref="TargetController.OnTactPassed"/>: там же, где босс
    /// копит свой удар, он копит и волну помощников, и стан морозит обе шкалы разом.
    /// </summary>
    public class BossSpell : BaseUnitSpell
    {
        private int _damge;
        private UnitConfig _simpleZombieConfig;
        private UnitConfig _ratZombieConfig;
        private int _spawnCooldownTacts;
        private int _twoPhaseAttackCooldownTacts;
        private int _threePhaseAttackCooldownTacts;
        private bool _startTwoPhase, _startThreePhase;
        private BattleController _battleController;

        private bool _startSpawn;
        private int _spawnTimerTacts;
        private UnitRuntime _owner;

        public BossSpell(int damage, UnitConfig simpleZombieConfig, UnitConfig ratZombieConfig,
                         int spawnCooldownTacts, int twoPhaseAttackCooldownTacts, int threePhaseAttackCooldownTacts)
        {
            _damge = damage;
            _simpleZombieConfig = simpleZombieConfig;
            _ratZombieConfig = ratZombieConfig;
            _spawnCooldownTacts = spawnCooldownTacts;
            _twoPhaseAttackCooldownTacts = twoPhaseAttackCooldownTacts;
            _threePhaseAttackCooldownTacts = threePhaseAttackCooldownTacts;
        }

        public override void DisposeSpell()
        {
            _startSpawn = false;
            if (_owner != null)
            {
                _owner.TargetController.OnTactPassed -= TickSpawn;
                _owner = null;
            }
        }

        public override void InitSpell(UnitRuntime owner, BattleController battleController)
        {
            _battleController = battleController;
            _owner = owner;
            _startTwoPhase = false;
            _startThreePhase = false;
            _startSpawn = false;
            _spawnTimerTacts = 0;
            owner.TargetController.OnAttack += () => owner.TargetController.CurrentTarget.TakeDamage(_damge);
            owner.TargetController.OnTactPassed += TickSpawn;
            owner.Health.OnChanged += (int _, int _) => CheckHP(owner);
            owner.Health.OnDied += DisposeSpell;
        }

        private void CheckHP(UnitRuntime owner)
        {
            if(owner.Health.CurrentHP == 0)
                return;
            if((float)owner.Health.CurrentHP / owner.Health.MaxHP * 100 <= 70 && !_startTwoPhase)
            {
                _startTwoPhase = true;
                owner.TargetController.SetNewTimerValue(_twoPhaseAttackCooldownTacts);
            }
            else if((float)owner.Health.CurrentHP / owner.Health.MaxHP * 100 <= 30 && !_startThreePhase)
            {
                _startThreePhase = true;
                owner.TargetController.SetNewTimerValue(_threePhaseAttackCooldownTacts);
                _startSpawn = true;
                // третья фаза начинается волной сразу, как и раньше, — дальше по тактам
                _spawnTimerTacts = _spawnCooldownTacts;
                SpawnPets();
            }
        }

        private void TickSpawn()
        {
            if (!_startSpawn || _spawnCooldownTacts <= 0)
                return;

            _spawnTimerTacts--;
            if (_spawnTimerTacts > 0)
                return;

            _spawnTimerTacts = _spawnCooldownTacts;
            SpawnPets();
        }

        private void SpawnPets()
        {
            _battleController.AddEnemy(_ratZombieConfig);
            _battleController.AddEnemy(_ratZombieConfig);
            _battleController.AddEnemy(_simpleZombieConfig);
        }
    }
}
