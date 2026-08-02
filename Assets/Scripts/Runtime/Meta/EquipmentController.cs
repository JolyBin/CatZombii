using Core.Spells;
using Meta.UI;
using Utility.Services.UI;

namespace Meta
{
    /// <summary>
    /// Окно книги героя — оно же будущий экран колоды (docs/10 §13.2).
    ///
    /// Сегодня окно только ПОКАЗЫВАЕТ книгу целиком. Когда у него появятся кнопки
    /// «взять / убрать», ему понадобится не книга, а <see cref="HeroLoadout"/>: в нём
    /// лежат и купленное, и экипированное, и оба закона — слоты и префиксы. Поэтому
    /// колода резолвится здесь и отдаётся наружу готовой (<see cref="CurrentLoadout"/>),
    /// а не собирается окном из профиля: правило, размазанное по экранам, разъезжается
    /// первым.
    /// </summary>
    public class EquipmentController
    {

        private IUIService _uiService;
        private UIEquipmentWindow _window;
        private readonly MetaController _meta;

        public EquipmentController(IUIService uiService, MetaController meta)
        {
            _uiService = uiService;
            _meta = meta;
        }

        /// <summary>Колода героя, чью книгу сейчас показывают. <c>null</c> — окно закрыто.</summary>
        public HeroLoadout CurrentLoadout { get; private set; }

        public void OpenWindow(Book selectedBook)
        {
            CurrentLoadout = _meta.LoadoutOf(selectedBook);

            _window = _uiService.Show<UIEquipmentWindow>();
            _window.OnBackButtonClick += CloseWidow;
            _window.SetBook(selectedBook);

        }

        public void CloseWidow()
        {
            _window.Hide();
            CurrentLoadout = null;
        }
    }
}
