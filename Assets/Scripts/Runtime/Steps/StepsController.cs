using Core.Battle;
using Core.Flask;
using Core.Spells;
using Core.Steps.UI;
using Cysharp.Threading.Tasks;
using Meta;
using System.Threading;
using UnityEngine;
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
        }

        private void Exit()
        {
            if (_partyCts != null && !_partyCts.IsCancellationRequested)
                _partyCts.Cancel();

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

        private void ShowWinWindow()
        {
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
            _loseWindow = _uiService.Show<UILoseWindow>();
            _loseWindow.OnClickContinueButton += () =>
            {
                _loseWindow.Hide();
                Exit();
            };
        }
    }
}
