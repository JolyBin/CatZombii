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
        private BattleConfig[] _battleConfigs;

        private UIHomeWindow _homeWindow;
        private StepsController _stepsController;
        private HeroController _heroController;

        private int _configIndex;

        public HomeController(IUIService uiService, Book startBook, BattleConfig[] battleConfigs)
        {
            _configIndex = 0;
            _uiService = uiService;
            _heroController = new HeroController(_uiService, startBook);
            _battleConfigs = battleConfigs;
            OpenWindow();
        }

        public void OpenWindow()
        {
            _homeWindow = _uiService.Show<UIHomeWindow>();
            _homeWindow.SetLevelValue(_configIndex + 1);
            _homeWindow.OnClickPlayButton += StartGame;
            _homeWindow.OnClickCharactersButton += OpenHeroesWindow;
        }

        public void AddConfigIndex()
        {
            if (_configIndex < _battleConfigs.Length - 1)
                _configIndex++;
        }

        private void StartGame()
        {
            _homeWindow.Hide();
            _stepsController = new StepsController(_uiService, _heroController.SeveBook, this, _battleConfigs[_configIndex]);
            _stepsController.Init();
        }

        private void OpenHeroesWindow()
        {
            _heroController.OpenWindow();
        }
    }
}
