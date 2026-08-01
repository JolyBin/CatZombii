using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace Core.Battle
{
    [CreateAssetMenu(fileName = "Boss Spell Config", menuName = "Battle Configs/Create Boss Spell Config")]
    public class BossSpellConfig : BaseUnitSpellConfig
    {
        [SerializeField] private int _damage;
        [SerializeField] private UnitConfig _simbpleZombieConfig;
        [SerializeField] private UnitConfig _ratZombieConfig;
        [SerializeField] private int _spawnColldawn = 10000;
        [SerializeField] private int _twoPhaseAttackCooldown = 3500;
        [SerializeField] private int _threePhaseAttackCooldown = 2500;

        public override BaseUnitSpell GetUnitSpell() => new BossSpell(_damage, _simbpleZombieConfig, _ratZombieConfig, _spawnColldawn, 
                                                                      _twoPhaseAttackCooldown, _threePhaseAttackCooldown);
    }

    public class BossSpell : BaseUnitSpell
    {
        private int _damge;
        private UnitConfig _simpleZombieConfig;
        private UnitConfig _ratZombieConfig;
        private int _spawnColldawn;
        private int _twoPhaseAttackCooldown;
        private int _threePhaseAttackCooldown;
        private bool _startTwoPhase, _startThreePhase;
        private BattleController _battleController;

        private bool _startSpawn;


        public BossSpell(int damage, UnitConfig simpleZombieConfig, UnitConfig ratZombieConfig, 
                         int spawnColdown, int twoPhaseAttackCooldown, int threePhaseAttackCooldown)
        {
            _damge = damage;
            _simpleZombieConfig = simpleZombieConfig;
            _ratZombieConfig = ratZombieConfig;
            _spawnColldawn = spawnColdown;
            _twoPhaseAttackCooldown = twoPhaseAttackCooldown;
            _threePhaseAttackCooldown = threePhaseAttackCooldown;
        }

        public override void DisposeSpell()
        {
            _startSpawn = false;
        }

        public override void InitSpell(UnitRuntime owner, BattleController battleController)
        {
            _battleController = battleController;
            _startTwoPhase = false;
            _startThreePhase = false;
            _startSpawn = false;
            owner.TargetController.OnAttack += () => owner.TargetController.CurrentTarget.TakeDamage(_damge);
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
                owner.TargetController.SetNewTimerValue(_twoPhaseAttackCooldown);
            }
            else if((float)owner.Health.CurrentHP / owner.Health.MaxHP * 100 <= 30 && !_startThreePhase)
            {
                _startThreePhase = true;
                owner.TargetController.SetNewTimerValue(_threePhaseAttackCooldown);
                _startSpawn = true;
                SpawnPets(_battleController.BattleToken).Forget();
            }
        }

        private async UniTaskVoid SpawnPets(CancellationToken token)
        {
            try
            {
                while (_startSpawn && !token.IsCancellationRequested)
                {
                    _battleController.AddEnemy(_ratZombieConfig);
                    _battleController.AddEnemy(_ratZombieConfig);
                    _battleController.AddEnemy(_simpleZombieConfig);

                    bool isCanceled = await UniTask.Delay(_spawnColldawn, cancellationToken: token).SuppressCancellationThrow();
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
