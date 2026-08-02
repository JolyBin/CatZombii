using Core.Spells;
using Meta.UI;
using Utility.Services.UI;


namespace Meta
{
    public class HeroController
    {
        /// <summary>
        /// Книга выбранного героя. Осталась ради вызывающих; истина — <c>MetaController</c>,
        /// у которого она же и лежит в <c>Saves.Profile.HeroId</c>.
        /// </summary>
        public Book SeveBook => _meta.CurrentBook;

        private IUIService  _uiService;
        private UIHeroesWindow _uiHeroesWindow;
        private Book _currentSelectedBook;

        private readonly MetaController _meta;
        private EquipmentController _equipmentController;

        public HeroController(IUIService uiService, MetaController meta)
        {
            _uiService = uiService;
            _meta = meta;
            _equipmentController = new(uiService, meta);
        }

        /// <summary>Мета — для окна героев: какие книги открыты, какая выбрана (docs/10 §13.4).</summary>
        public MetaController Meta => _meta;

        /// <summary>
        /// Колода героя, чью карточку сейчас смотрят. Точка входа окна экипировки.
        /// </summary>
        public HeroLoadout LoadoutOf(Book book) => _meta.LoadoutOf(book);

        public void OpenWindow()
        {
            _uiHeroesWindow = _uiService.Show<UIHeroesWindow>();
            _currentSelectedBook = _uiHeroesWindow.UIHeroList[0].HeroBook;
            foreach(UIHero uiHero in _uiHeroesWindow.UIHeroList)
            {
                if (_meta.CurrentBook == uiHero.HeroBook)
                {
                    _currentSelectedBook = uiHero.HeroBook;
                    uiHero.Init(true);
                }
                else
                {
                    uiHero.Init(false);
                }
                uiHero.OnHeroSelected += (value) => SelectedHero(value, uiHero);
                uiHero.OnInfoButtonClick += (value) => _equipmentController.OpenWindow(value);
            }

            _uiHeroesWindow.OnClickHomeButton += HideWindow;

            _uiHeroesWindow.OnClickSaveButton += SaveSelectedHero;
        }

        /// <summary>
        /// ТОЧКА СОХРАНЕНИЯ №2 — игрок нажал «сохранить» в окне героев.
        ///
        /// Почему здесь, а не на переключении тумблера: тумблер — это ещё не выбор,
        /// игрок листает героев и сравнивает. Кнопка «сохранить» уже названа сохранением,
        /// обманывать её значение нельзя; а писать в IndexedDB на каждое переключение
        /// тумблера значит платить за то, чего игрок не просил.
        ///
        /// Повторное нажатие ничего не стоит: если герой не менялся, записи не будет.
        /// Закрытого героя выбрать нельзя — герои открываются прогрессом (docs/10 §13.4),
        /// и окно обязано показывать это ДО клика, а не отказом после.
        /// </summary>
        private void SaveSelectedHero()
        {
            _meta.TrySelectHero(_currentSelectedBook);
            HideWindow();
        }

        private void SelectedHero(bool value, UIHero uiHero)
        {
            if(value)
                _currentSelectedBook = uiHero.HeroBook;
        }

        private void HideWindow()
        {
            _uiHeroesWindow.Hide();

        }

    }
}
