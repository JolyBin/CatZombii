using Core.Battle;
using Core.Spells;
using Core.Steps;
using Meta.UI;
using UnityEngine;
using Utility.Services.Saves;
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

        /// <summary>
        /// Текущий уровень. Больше НЕ источник истины: истина —
        /// <c>Saves.Profile.LevelIndex</c>, а это поле её рабочая копия на время сессии.
        /// Раньше здесь и жил весь прогресс игры — блокер №1 роадмапа (docs/07):
        /// перезагрузил вкладку, начал с первого уровня.
        /// </summary>
        private int _configIndex;

        public HomeController(IUIService uiService, Book startBook, BattleConfig[] battleConfigs)
        {
            _uiService = uiService;
            _battleConfigs = battleConfigs;
            _heroController = new HeroController(_uiService, startBook);
            _configIndex = ClampToLevels(Saves.Profile.LevelIndex);
            // Починку возвращаем в профиль, иначе она живёт только до конца сессии
            // и «уровень 1000» всплывал бы снова при каждом запуске. Записи на диск
            // здесь НЕТ намеренно: старт игры — не точка сохранения, а исправленное
            // значение уедет на диск при первом же настоящем сохранении.
            Saves.Profile.LevelIndex = _configIndex;
            OpenWindow();
        }

        public void OpenWindow()
        {
            _homeWindow = _uiService.Show<UIHomeWindow>();
            _homeWindow.SetLevelValue(_configIndex + 1);
            _homeWindow.OnClickPlayButton += StartGame;
            _homeWindow.OnClickCharactersButton += OpenHeroesWindow;
        }

        /// <summary>
        /// ТОЧКА СОХРАНЕНИЯ №1 — победа. Зовётся из <c>StepsController.ShowWinWindow</c>
        /// по кнопке «продолжить», то есть ровно один раз на пройденный уровень.
        ///
        /// Почему именно здесь, а не «на каждый чих»: это ЕДИНСТВЕННОЕ место, где меняется
        /// прогресс уровней, и оно же — то место, потерю которого игрок заметит.
        /// В WebGL запись идёт в IndexedDB и стоит заметно дороже присваивания, поэтому
        /// сохраняться на каждом такте или на каждой волне было бы платой без покупки.
        /// </summary>
        public void AddConfigIndex()
        {
            if (_configIndex >= _battleConfigs.Length - 1)
                return;

            _configIndex++;
            Saves.Profile.LevelIndex = _configIndex;
            Saves.RequestSave($"уровень {_configIndex} пройден");
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

        /// <summary>
        /// Сейв — ВНЕШНИЕ данные, и уровней в сборке может стать меньше, чем было
        /// у игрока (вырезали уровень, откатили релиз). Индекс за границей массива —
        /// это <c>IndexOutOfRangeException</c> на кнопке «играть», то есть игра,
        /// которая не запускается и не чинится ничем, кроме очистки данных сайта.
        /// </summary>
        private int ClampToLevels(int levelIndex)
        {
            if (_battleConfigs == null || _battleConfigs.Length == 0)
                return 0;

            int clamped = Mathf.Clamp(levelIndex, 0, _battleConfigs.Length - 1);
            if (clamped != levelIndex)
            {
                Debug.LogWarning($"[Saves] В сейве уровень {levelIndex + 1}, а в сборке их " +
                                 $"{_battleConfigs.Length}. Ставим {clamped + 1}.");
            }
            return clamped;
        }
    }
}
