using Core.Spells;
using Meta.UI;
using Utility.Services.UI;


namespace Meta
{
    public class HeroController
    {
        public Book SeveBook => _saveBook;

        private IUIService  _uiService;
        private UIHeroesWindow _uiHeroesWindow;
        private Book _currentSelectedBook;
        private Book _saveBook;

        public HeroController(IUIService uiService, Book startBook)
        {
            _uiService = uiService;
            _saveBook = startBook;
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
            }

            _uiHeroesWindow.OnClickHomeButton += HideWindow;

            _uiHeroesWindow.OnClickSaveButton += () =>
            {
                _saveBook = _currentSelectedBook;
                HideWindow();
            };
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
