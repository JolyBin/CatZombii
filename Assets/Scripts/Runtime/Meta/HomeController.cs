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

        /// <summary>
        /// Мета целиком: карта, кошелёк, покупки, колоды. Создаётся здесь, потому что
        /// здесь же начинается и кончается сессия главного экрана.
        /// </summary>
        private readonly MetaController _meta;

        /// <summary>
        /// Узел, с которого игрок сейчас пойдёт в бой. Это НЕ прогресс — прогресс живёт
        /// в <c>Saves.Profile.ClearedNodes</c> и читается через <see cref="MetaController.Map"/>.
        ///
        /// Раньше на этом месте был <c>_configIndex</c>, и он был сразу и выбором,
        /// и прогрессом, и тупиком: рос на победе, упирался в последний уровень
        /// и никогда не сбрасывался (docs/10 §13.4). Теперь узлы перепроходимы,
        /// и «какой узел выбран» — это состояние ЭКРАНА, живущее до следующего клика.
        /// </summary>
        private int _selectedNode;

        public HomeController(IUIService uiService, Book startBook, BattleConfig[] battleConfigs)
        {
            _uiService = uiService;
            _battleConfigs = battleConfigs;
            _meta = new MetaController(startBook, battleConfigs);
            _heroController = new HeroController(_uiService, _meta);
            _selectedNode = _meta.Map.NextNode;
            OpenWindow();
        }

        /// <summary>Мета для экранов карты, колоды и лавки. Единственный вход к прогрессу.</summary>
        public MetaController Meta => _meta;

        /// <summary>Узел, выбранный на карте. Пока экрана карты нет — первый непройденный.</summary>
        public int SelectedNode => _selectedNode;

        public void OpenWindow()
        {
            _homeWindow = _uiService.Show<UIHomeWindow>();
            _homeWindow.SetLevelValue(_selectedNode + 1);
            _homeWindow.OnClickPlayButton += StartGame;
            _homeWindow.OnClickCharactersButton += OpenHeroesWindow;
        }

        /// <summary>
        /// ВЫБОР УЗЛА НА КАРТЕ. Точка входа для экрана карты (docs/10 §13.4): узлы
        /// перепроходимы, поэтому выбрать можно любой открытый, а не только следующий.
        /// </summary>
        /// <returns><c>false</c> — узел закрыт или его нет; экран обязан оставить выбор как был.</returns>
        public bool TrySelectNode(int node)
        {
            if (!_meta.Map.IsUnlocked(node))
                return false;

            _selectedNode = node;
            _homeWindow?.SetLevelValue(_selectedNode + 1);
            return true;
        }

        /// <summary>Выбрать узел и сразу пойти в бой — то, что делает клик по узлу карты.</summary>
        public bool TryStartNode(int node)
        {
            if (!TrySelectNode(node))
                return false;

            StartGame();
            return true;
        }

        /// <summary>
        /// ПОБЕДА НА УЗЛЕ. Зовётся из <c>StepsController.ShowWinWindow</c> по кнопке
        /// «продолжить», то есть ровно один раз на выигранный бой.
        ///
        /// Что изменилось против прежнего <c>AddConfigIndex</c>: прогресс больше не
        /// «следующий уровень», а «узел пройден». Перепрохождение уже пройденного узла
        /// прогресс не двигает и записи на диск не стоит — но событие о победе всё равно
        /// случается, потому что награда за перепрохождение есть (40% по §15.3).
        ///
        /// ⚠️ ЭТО ЕДИНСТВЕННОЕ МЕСТО, ГДЕ НАЧИСЛЯЕТСЯ НАГРАДА ЗА БОЙ. Кнопка «×2 за
        /// рекламу» (docs/10 §10) не должна считать награду заново — ей достаточно
        /// добавить столько же ещё раз: <c>Meta.AddCoins(reward, "×2 за рекламу")</c>.
        /// Второй счётчик награды разъедется с первым при первой же правке §15.3.
        /// </summary>
        /// <returns>Сколько клубков начислено за бой — это же число показывает экран победы.</returns>
        public int RegisterNodeCleared()
        {
            int node = _selectedNode;
            _meta.Map.TryRegisterClear(node, out bool firstClear);

            int reward = MetaEconomy.NodeReward(_meta.Map.ChapterOf(node), _meta.Map.IsBoss(node), firstClear);
            _meta.AddCoins(reward, $"награда за узел {node + 1}");

            // Экран карты появится позже; пока сохраняем прежнее поведение главного
            // экрана — после победы он предлагает следующий узел.
            _selectedNode = _meta.Map.NextNode;
            return reward;
        }

        /// <summary>
        /// СВЯЗАТЬ ЭКРАНЫ МЕТЫ С ДАННЫМИ. Одна точка входа для карты, колоды и лавки:
        /// возвращённый <see cref="MetaScreensBinding"/> уже умеет собирать все три
        /// модели и применять клики по законам docs/10 §13.
        ///
        /// Освобождение — на вызывающем: <c>binding.ClearAction()</c> вместе с
        /// <c>MetaScreensController.Exit()</c>.
        /// </summary>
        public MetaScreensBinding CreateScreensBinding() => new MetaScreensBinding(this);

        private void StartGame()
        {
            BattleConfig config = _meta.Map.ConfigOf(_selectedNode);
            if (config == null)
            {
                Debug.LogError($"[Мета] Узла {_selectedNode + 1} нет в сборке — играть нечем.");
                return;
            }

            _homeWindow.Hide();

            // ЗАКОН §13.1: в бой едет КОЛОДА, а не книга. Отсюда «каждое новое заклинание
            // делает пазл труднее» — пул стихий колб выводится из экипированного набора.
            SpellDeck deck = _meta.CurrentLoadout.BuildDeck();
            _stepsController = new StepsController(_uiService, deck, this, config);
            _stepsController.Init();
        }

        private void OpenHeroesWindow()
        {
            _heroController.OpenWindow();
        }
    }
}
