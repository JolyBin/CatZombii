using Core.Battle;
using Core.Spells;
using Core.Steps;
using Meta.UI;
using UnityEngine;
using Utility.Services.UI;

namespace Meta
{
    public class HomeController
    {
        private IUIService _uiService;
        private BattleConfig _battleConfig;

        private UIHomeWindow _homeWindow;
        private StepsController _stepsController;
        private HeroController _heroController;

        public HomeController(IUIService uiService, Book startBook, BattleConfig battleConfig)
        {
            _uiService = uiService;
            _heroController = new HeroController(_uiService, startBook);
            _battleConfig = battleConfig;
            OpenWindow();
        }

        public void OpenWindow()
        {
            _homeWindow = _uiService.Show<UIHomeWindow>();
            _homeWindow.OnClickPlayButton += StartGame;
            _homeWindow.OnClickCharactersButton += OpenHeroesWindow;
        }

        private void StartGame()
        {
            _homeWindow.Hide();
            _stepsController = new StepsController(_uiService, _heroController.SeveBook, this, _battleConfig);
            _stepsController.Init();
        }

        private void OpenHeroesWindow()
        {
            _heroController.OpenWindow();
        }
    }
}
