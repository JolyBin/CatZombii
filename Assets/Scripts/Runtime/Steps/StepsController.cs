using Core.Battle;
using Core.Flask;
using Core.Spells;
using Core.Steps.UI;
using Meta;
using System;
using System.Threading;
using Utility.Services.UI;

namespace Core.Steps
{
    public class StepsController
    {
        private readonly FlaskController _flaskController;
        private readonly IUIService _uiService;
        private readonly TableController _tableController;

        private UIBattleWindow _window;
        private UIWinWindow _winWindow;
        private UILoseWindow _loseWindow;
        private HomeController _homeController;
        private BattleController _battleController;
        private Book _currentBook;

        /// <summary>
        /// Часы мира: единственный источник времени партии (docs/10 §0.2).
        /// Собираются здесь, потому что здесь же лежат все три системы, которые
        /// такт затрагивает, — колбы (источник действий), котёл и бой.
        /// </summary>
        private WorldClock _worldClock;

        /// <summary>
        /// Один и тот же обработчик на оба действия игрока — перелив и варку.
        /// Хранится полем ради парной отписки в <see cref="Exit"/>.
        /// </summary>
        private Action _tickAction;

        /// <summary>
        /// Токен жизни партии: отменяется в Exit(), гасит все отложенные эффекты боя.
        /// </summary>
        private CancellationTokenSource _partyCts;


        public StepsController(IUIService uIService, Book currentBook, HomeController homeController, BattleConfig currentlevel)
        {
            _currentBook = currentBook;
            _flaskController = new FlaskController(uIService, currentBook.UniqElements);
            _tableController = new TableController(currentBook, _flaskController, uIService);
            _uiService = uIService;
            _homeController = homeController;
            _battleController = new BattleController(uIService, currentlevel, currentBook, _tableController);
        }

        public void Init()
        {
            _partyCts = new CancellationTokenSource();
            _window = _uiService.Show<UIBattleWindow>();
            _window.Init(_currentBook);
            _flaskController.Init();
            _window.OnClickHomeButton += Exit;
            _flaskController.SubscribeToMove();
            _battleController.OnAllEnemyDie += ShowWinWindow;
            _battleController.OnHeroDie += ShowLoseWindow;
            _battleController.Init(_partyCts.Token);
            _tableController.Init();

            // ЕДИНСТВЕННЫЙ ВХОД ТАКТА. Оба действия игрока идут в один и тот же метод,
            // а порядок обработки внутри такта живёт целиком в WorldClock.Tick —
            // ни котёл, ни бой не подписаны на MoveCommand напрямую и не могут
            // «переставить» друг друга порядком подписки.
            _worldClock = new WorldClock(_tableController, _battleController);
            _tickAction = _worldClock.Tick;
            _flaskController.MoveCommand += _tickAction;
            _tableController.OnBrewCommand += _tickAction;
            _worldClock.Start();
        }

        private void Exit()
        {
            if (_partyCts != null && !_partyCts.IsCancellationRequested)
                _partyCts.Cancel();

            if (_tickAction != null)
            {
                _flaskController.MoveCommand -= _tickAction;
                _tableController.OnBrewCommand -= _tickAction;
                _tickAction = null;
            }
            _worldClock?.Stop();
            _worldClock = null;

            _battleController.OnAllEnemyDie -= ShowWinWindow;
            _battleController.OnHeroDie -= ShowLoseWindow;

            _window.OnClickHomeButton -= Exit;
            _window.Hide();
            _flaskController.Exit();
            _tableController.Exit();
            _battleController.Exit();

            // связанный токен боя уже освобождён внутри BattleController.Exit()
            _partyCts?.Dispose();
            _partyCts = null;

            _homeController.OpenWindow();
        }

        /// <summary>
        /// Партия кончилась любым исходом: мир останавливается и перестаёт принимать
        /// действия. Это и есть вторая половина бага №12 — пазл больше не живёт
        /// под окном итога. Пошаговость делает лечение полным: остановить приём
        /// действий = остановить мир, отдельного «замораживателя» не требуется.
        /// </summary>
        private void FinishParty()
        {
            _worldClock?.Stop();
            _flaskController.LockInput();
            _tableController.LockInput();
        }

        private void ShowWinWindow()
        {
            FinishParty();
            _winWindow = _uiService.Show<UIWinWindow>();
            _winWindow.OnClickContinueButton += () =>
            {
                _winWindow.Hide();
                _homeController.AddConfigIndex();
                Exit();
            };
        }

        private void ShowLoseWindow()
        {
            FinishParty();
            _loseWindow = _uiService.Show<UILoseWindow>();
            _loseWindow.OnClickContinueButton += () =>
            {
                _loseWindow.Hide();
                Exit();
            };
        }
    }
}
