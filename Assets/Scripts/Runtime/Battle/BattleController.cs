using Core.Spells;
using Core.Steps;
using Core.Steps.UI;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Utility.Services.UI;

namespace Core.Battle
{
    /// <summary>
    /// Бой. ПОШАГОВЫЙ: собственного времени у него нет, такт приходит из
    /// <see cref="WorldClock"/> — то есть от действия игрока (docs/10 §0.2).
    /// </summary>
    public class BattleController : ITickable
    {
        public event Action OnHeroDie;
        public event Action OnAllEnemyDie;
        public event Action<Health> OnAddEnemy;
        public event Action<Health> OnAddFriend;

        /// <summary>
        /// Такт мира дошёл до боя. Точка подключения для ДЛЯЩИХСЯ эффектов игрока
        /// (регенерация), у которых свой ритм и нет носителя-юнита: юниту хватило бы
        /// <see cref="TargetController.OnTactPassed"/>, а эффекту на герое цепляться не за что.
        ///
        /// Без этого события длящийся эффект остался бы единственным куском real-time
        /// в пошаговой игре: лечение капало бы, пока игрок думает, то есть «постоять
        /// и подышать» лечило бы бесплатно — ровно та дыра, ради закрытия которой
        /// и переводились остальные таймеры.
        /// </summary>
        public event Action OnTact;

        public Health HeroHealth { get; private set; }
        public UnitRuntime[] FriendlySquad => _friendlyList.ToArray();
        public UnitRuntime[] EnemySquad => _enemyList.ToArray();

        public UnitRuntime HeroTarget { get; private set; }

        /// <summary>
        /// Токен жизни боя. Отменяется при смерти героя, победе и выходе из партии.
        /// </summary>
        public CancellationToken BattleToken => _battleCts == null ? CancellationToken.None : _battleCts.Token;

        private IUIService _uIService;
        private List<UnitRuntime> _friendlyList;
        private List<UnitRuntime> _enemyList;
        private UIBattleWindow _battleWindow;

        private BattleConfig _currentLevel;
        private Book _playerConfig;
        private TableController _tableController;
        private int _currentWaveIndex;

        private CancellationTokenSource _battleCts;
        private bool _isBattleOver;
        private Action<BaseSpell> _applySpellAction;

        /// <summary>
        /// Токен партии, из которого делается связанный токен боя. Хранится полем ради
        /// <see cref="ReviveHero"/>: воскрешение — это второй бой в той же партии,
        /// и ему нужен НОВЫЙ токен боя, но привязанный к той же партии.
        /// </summary>
        private CancellationToken _partyToken;

        /// <summary>
        /// Снимок отряда на время такта. Атака убивает юнита прямо внутри обхода
        /// (Health.OnDied → Remove из списка), поэтому ходить по живому списку нельзя.
        /// Буфер переиспользуется: такт случается на каждое действие игрока.
        /// </summary>
        private readonly List<UnitRuntime> _tickBuffer = new();

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

        public void Init(CancellationToken partyToken)
        {
            _currentWaveIndex = 0;
            _isBattleOver = false;
            _partyToken = partyToken;
            _battleCts = CancellationTokenSource.CreateLinkedTokenSource(partyToken);

            HeroHealth = new Health(_playerConfig.HP, 0);
            _battleWindow.SetHero(_playerConfig);
            _battleWindow.SetHealth(HeroHealth.CurrentHP, HeroHealth.MaxHP);
            BindHeroHealth();

            _applySpellAction = (BaseSpell spell) => ApplySpell(spell).Forget();
            _tableController.OnSuccessfulMerge += _applySpellAction;
            StartWave();
        }

        public void AddEnemy(UnitConfig unit)
        {
            if (_isBattleOver)
                return;

            if (_enemyList.Count >= _battleWindow.EnemyPositionsCount)
            {
                _enemyList[Math.Min(1, _enemyList.Count - 1)].Health.Die();
            }
            UnitRuntime unitRuntime = new UnitRuntime(unit, this);
            unitRuntime.UIUnit.OnSelectClickButton += () => SelectEnemyTarget(unitRuntime);
            _enemyList.Add(unitRuntime);
            OnAddEnemy?.Invoke(unitRuntime.Health);

            if(unit.AttackCooldownTacts > 0)
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
            unitPosition?.SetUnit(unitRuntime.UIUnit.transform as RectTransform);

            unitRuntime.Health.OnDied += () =>
            {

                OnAddFriend -= unitRuntime.TargetController.AddTarget;
                _enemyList.Remove(unitRuntime);
                if (HeroTarget == unitRuntime)
                    SelectedLastTarget();
                unitRuntime.Dispose();
                unitPosition?.SetFree();

                if (_enemyList.Count == 0 && !_isBattleOver)
                {
                    _currentWaveIndex++;
                    if(_currentLevel.Waves.Length == _currentWaveIndex)
                    {
                        EndBattle();
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
            if (_isBattleOver)
                return;

            if (_friendlyList.Count >= _battleWindow.FriendlyPositionsCount)
            {
                _friendlyList[0].Health.Die();
            }
            UnitRuntime unitRuntime = new UnitRuntime(unit, this);
            _friendlyList.Add(unitRuntime);
            OnAddFriend?.Invoke(unitRuntime.Health);

            if (unit.AttackCooldownTacts > 0)
            {
                foreach (var target in _enemyList)
                {
                    unitRuntime.TargetController.AddTarget(target.Health);
                }
                OnAddEnemy += unitRuntime.TargetController.AddTarget;
                unitRuntime.TargetController.StartAttack();
            }

            UIUnitPosition unitPosition = _battleWindow.SetFriendPosition();
            unitPosition?.SetUnit(unitRuntime.UIUnit.transform as RectTransform);

            unitRuntime.Health.OnDied += () =>
            {
                unitPosition?.SetFree();
                OnAddEnemy -= unitRuntime.TargetController.AddTarget;
                _friendlyList.Remove(unitRuntime);
                unitRuntime.Dispose();
            };
        }

        /// <summary>
        /// Такт боя: вторая фаза такта мира (порядок и его обоснование — в WorldClock.Tick).
        ///
        /// Порядок внутри фазы:
        ///   1) ДЛЯЩИЕСЯ ЭФФЕКТЫ ИГРОКА (<see cref="OnTact"/>, регенерация). Игрок за них
        ///      уже заплатил ходом варки — лечение обязано успеть до удара, от которого
        ///      его и варили, иначе «сварить лечение на трёх HP» проигрывается всегда.
        ///   2) ВРАГИ.
        ///   3) СОЮЗНИКИ. Враг, доживший до своего удара, обязан ударить в том же такте,
        ///      в котором игрок сделал ход, — иначе призыв питомца работал бы как
        ///      бесплатный «блок» уже занесённого удара, а телеграфа у ударов нет.
        /// </summary>
        public void Tick()
        {
            if (_isBattleOver)
                return;

            OnTact?.Invoke();
            if (_isBattleOver)
                return;

            TickSquad(_enemyList);
            TickSquad(_friendlyList);
        }

        private void TickSquad(List<UnitRuntime> squad)
        {
            _tickBuffer.Clear();
            _tickBuffer.AddRange(squad);

            foreach (UnitRuntime unit in _tickBuffer)
            {
                // бой мог закончиться прямо в этом обходе — добивать уже некого
                if (_isBattleOver)
                    return;
                unit.TargetController.Tick();
            }
        }

        private async UniTaskVoid ApplySpell(BaseSpell spell)
        {
            try
            {
                await spell.ApplySpell(this, BattleToken);
            }
            catch (OperationCanceledException)
            {
                // штатное завершение: бой закончился раньше заклинания
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void HeroDie()
        {
            EndBattle();
            OnHeroDie?.Invoke();
        }

        /// <summary>
        /// ВОСКРЕСИТЬ ГЕРОЯ И ПРОДОЛЖИТЬ БОЙ. Вход для rewarded-крючка «продолжить после
        /// поражения» (docs/09 пункт 8; docs/10 §10: «кот встаёт с 50% HP, волна сохраняется»).
        ///
        /// ЗДЕСЬ И ЖИВЁТ ПЕРЕПРИВЯЗКА — та самая, без которой воскрешение бессмысленно.
        /// Смерть героя не просто обнулила HP, она разобрала три связи, и вернуть их
        /// умеет только тот, кто их ставил, то есть этот класс:
        ///
        ///   1) ЦЕЛИ ВРАГОВ. Каждый враг на смерти героя вычеркнул его из своего списка
        ///      целей (<c>TargetController.AddTarget</c> вешает такую отписку). Не вернуть —
        ///      и воскресшего героя никто не бьёт: бой становится непроигрываемым,
        ///      а рекламный крючок — чит-кнопкой.
        ///   2) ЖИЗНЬ БОЯ. <see cref="EndBattle"/> отменил токен боя и остановил обе стороны.
        ///      Токен отменённым остаётся навсегда — нужен новый, связанный с той же партией,
        ///      иначе первое же заклинание отменится, не начавшись.
        ///   3) ПОЛОСКА ЗДОРОВЬЯ. Перепривязывается принудительно
        ///      (<see cref="BindHeroHealth"/>) и сразу же обновляется числом: полагаться
        ///      на «она вроде и не отписывалась» здесь нельзя — цена ошибки как раз и есть
        ///      «жив в модели, мёртв на экране».
        ///
        /// Волна НЕ перезапускается: живые враги остаются с текущим HP и текущими
        /// таймерами. Это и есть «волна сохраняется» — игрок покупает продолжение
        /// той же схватки, а не более лёгкую её версию.
        /// </summary>
        /// <param name="percentOfMaxHP">Сколько HP вернуть, в процентах от максимума.</param>
        /// <returns><c>false</c> — воскрешать некого (герой жив или партия уже вышла).</returns>
        public bool ReviveHero(int percentOfMaxHP)
        {
            if (HeroHealth == null || !HeroHealth.IsDead)
                return false;

            // 2) жизнь боя — новый токен вместо отменённого
            _battleCts?.Dispose();
            _battleCts = CancellationTokenSource.CreateLinkedTokenSource(_partyToken);
            _isBattleOver = false;

            if (!HeroHealth.RevivePercent(percentOfMaxHP))
                return false;

            // 3) полоска здоровья
            BindHeroHealth();
            _battleWindow.SetHealth(HeroHealth.CurrentHP, HeroHealth.MaxHP);

            // 1) цели врагов + снятие паузы с обеих сторон
            foreach (UnitRuntime enemy in _enemyList)
            {
                enemy.TargetController.AddTarget(HeroHealth);
                enemy.TargetController.ContinueAttack();
            }
            foreach (UnitRuntime friend in _friendlyList)
            {
                friend.TargetController.ContinueAttack();
            }

            Debug.Log($"[Battle] Герой воскрешён: {HeroHealth.CurrentHP}/{HeroHealth.MaxHP} HP, " +
                      $"волна {_currentWaveIndex + 1}, врагов на поле {_enemyList.Count}.");
            return true;
        }

        /// <summary>
        /// Привязка героя к окну боя. Отдельным методом, потому что зовётся ДВАЖДЫ —
        /// на старте боя и на воскрешении. Парная отписка перед подпиской делает вызов
        /// идемпотентным: повторное воскрешение не должно давать два обновления полоски
        /// на один удар (и два вызова <see cref="HeroDie"/> на одну смерть).
        /// </summary>
        private void BindHeroHealth()
        {
            HeroHealth.OnChanged -= _battleWindow.SetHealth;
            HeroHealth.OnDied -= HeroDie;
            HeroHealth.OnChanged += _battleWindow.SetHealth;
            HeroHealth.OnDied += HeroDie;
        }

        /// <summary>
        /// Бой закончен: останавливает обе стороны и все отложенные эффекты.
        /// </summary>
        private void EndBattle()
        {
            if (_isBattleOver)
                return;
            _isBattleOver = true;

            foreach (var item in _enemyList)
            {
                item.TargetController.StopAttack();
            }
            foreach (var item in _friendlyList)
            {
                item.TargetController.StopAttack();
            }

            CancelBattleToken();
        }

        private void CancelBattleToken()
        {
            if (_battleCts == null)
                return;
            if (!_battleCts.IsCancellationRequested)
                _battleCts.Cancel();
        }

        private void SelectEnemyTarget(UnitRuntime unitRuntime)
        {
            if (HeroTarget != null)
                HeroTarget.UIUnit.Selected(false);
            HeroTarget = unitRuntime;
            HeroTarget.UIUnit.Selected(true);
        }

        private void SelectedLastTarget()
        {
            if (_enemyList.Count == 0)
            {
                HeroTarget = null;
                return;
            }
            HeroTarget = _enemyList.Last();
            HeroTarget.UIUnit.Selected(true);
        }

        private void StartWave()
        {
            Wave currentWave = _currentLevel.Waves[_currentWaveIndex];
            _battleWindow.SetWave(_currentWaveIndex + 1, _currentLevel.Waves.Length);
            HeroHealth.Heal(currentWave.HealValue);
            foreach (UnitConfig unit in currentWave.UnitConfigs)
            {
                AddEnemy(unit);
            }

            SelectedLastTarget();
        }

        public void Exit()
        {
            _isBattleOver = true;
            CancelBattleToken();
            _battleCts?.Dispose();
            _battleCts = null;

            if (HeroHealth != null)
            {
                HeroHealth.OnChanged -= _battleWindow.SetHealth;
                HeroHealth.OnDied -= HeroDie;
            }
            if (_applySpellAction != null)
            {
                _tableController.OnSuccessfulMerge -= _applySpellAction;
                _applySpellAction = null;
            }

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
            // длящиеся эффекты не переживают партию: подписчик мог не досчитать свои такты
            OnTact = null;
            HeroTarget = null;
            _enemyList = new();
            _friendlyList = new();
        }
    }
}
