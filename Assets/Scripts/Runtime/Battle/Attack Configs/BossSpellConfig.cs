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

        // Наследие real-time: миллисекунды из ассета. В игру уходят уже переведёнными
        // в такты единственным множителем конверсии (WorldClock.MILLISECONDS_PER_TACT):
        // 10000 -> 10 ходов на волну помощников, 3500 -> 4 и 2500 -> 3 хода на удар.
        // Ручной override в тактах есть у обычных врагов (UnitConfig); боссу он не нужен,
        // пока его фазы разносятся тем же множителем.
        [SerializeField] private int _spawnColldawn = 10000;
        [SerializeField] private int _twoPhaseAttackCooldown = 3500;
        [SerializeField] private int _threePhaseAttackCooldown = 2500;

        public override BaseUnitSpell GetUnitSpell() => new BossSpell(_damage, _simbpleZombieConfig, _ratZombieConfig,
                                                                      WorldClock.TactsFromMilliseconds(_spawnColldawn),
                                                                      WorldClock.TactsFromMilliseconds(_twoPhaseAttackCooldown),
                                                                      WorldClock.TactsFromMilliseconds(_threePhaseAttackCooldown));
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
