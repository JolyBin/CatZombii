using Core.Battle;
using Core.Flask;
using Core.Spells;
using Core.Steps.UI;
using Cysharp.Threading.Tasks;
using Meta;
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
        private HomeController _homeController;
        private BattleController _battleController;
        private Book _currentBook;


        public StepsController(IUIService uIService, Book currentBook, HomeController homeController, BattleConfig currentlevel)
        {
            _currentBook = currentBook;
            _flaskController = new FlaskController(uIService, currentBook.UniqElements);
            _tableController = new TableController(currentBook, _flaskController, uIService);
            _uiService = uIService;
            _homeController = homeController;
            _battleController = new(uIService, currentlevel, currentBook);
        }

        public void Init()
        {
            _window = _uiService.Show<UIBattleWindow>();
            _window.Init(_currentBook);
            _flaskController.Init();
            _window.OnClickHomeButton += Exit;
            _flaskController.SubscribeToMove();
            _battleController.Init();
        }

        private void Exit()
        {
            _window.Hide();
            _flaskController.Exit();
            _tableController.Exit();
            _battleController.Exit();
            _homeController.OpenWindow();

        }
    }
}
