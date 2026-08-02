using Core.Spells;
using Meta.UI;
using UnityEngine;
using Utility.Services.Saves;
using Utility.Services.UI;


namespace Meta
{
    public class HeroController
    {
        public Book SeveBook => _saveBook;

        private IUIService  _uiService;
        private UIHeroesWindow _uiHeroesWindow;
        private Book _currentSelectedBook;

        /// <summary>
        /// Выбранный герой. Больше НЕ источник истины: истина — <c>Saves.Profile.HeroId</c>,
        /// а это поле её рабочая копия на время сессии (ассет по идентификатору из сейва).
        /// </summary>
        private Book _saveBook;
        private EquipmentController _equipmentController;

        public HeroController(IUIService uiService, Book startBook)
        {
            _uiService = uiService;
            _saveBook = ResolveSavedBook(startBook);
            _equipmentController = new(uiService);
        }

        public void OpenWindow()
        {
            _uiHeroesWindow = _uiService.Show<UIHeroesWindow>();
            _currentSelectedBook = _uiHeroesWindow.UIHeroList[0].HeroBook;
            foreach(UIHero uiHero in _uiHeroesWindow.UIHeroList)
            {
                if (_saveBook == uiHero.HeroBook)
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
        /// </summary>
        private void SaveSelectedHero()
        {
            if (_saveBook != _currentSelectedBook)
            {
                _saveBook = _currentSelectedBook;
                Saves.Profile.HeroId = _saveBook == null ? string.Empty : _saveBook.HeroId;
                Saves.RequestSave($"выбран герой «{Saves.Profile.HeroId}»");
            }
            HideWindow();
        }

        /// <summary>
        /// Достать из сейва героя, которым играли в прошлый раз.
        ///
        /// Любой сбой здесь — НЕ повод остаться без книги: без книги не собирается ни одна
        /// комбинация, то есть игра запустится, но играть в неё будет нельзя. Поэтому все
        /// три плохих случая (в сейве пусто, ассет переименовали, ассет удалили) ведут
        /// в одно место — герой по умолчанию из <c>GameManager._startBook</c>.
        /// </summary>
        private Book ResolveSavedBook(Book fallbackBook)
        {
            string savedHeroId = Saves.Profile.HeroId;
            if (string.IsNullOrEmpty(savedHeroId))
                return fallbackBook;

            Book savedBook = BookCatalog.Find(savedHeroId);
            if (savedBook != null)
                return savedBook;

            Debug.LogWarning($"[Saves] Героя «{savedHeroId}» из сейва нет среди книг " +
                             $"(Resources/{BookCatalog.RESOURCES_PATH}). Берём героя по умолчанию.");
            return fallbackBook;
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
