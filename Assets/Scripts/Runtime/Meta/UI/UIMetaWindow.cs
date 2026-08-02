using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.UI;
using Utility.UI;

namespace Meta.UI
{
    /// <summary>
    /// ОБЩЕЕ У ТРЁХ ЭКРАНОВ МЕТЫ — карты, колоды и лавки. Сегодня общее ровно одно:
    /// ПОКАЗ ОТКАЗА.
    ///
    /// ═══ ЗАЧЕМ ═══
    ///
    /// Законы колоды (docs/10 §13.2) отказывают часто и по-разному: «сначала возьми
    /// префикс», «нет свободного слота», «не по карману». Формулировки давно написаны
    /// и локализованы (<c>HeroLoadout.Describe</c>), но уходили только в консоль —
    /// то есть игрок видел, что карточка не надевается, и не понимал почему.
    /// Молчаливый отказ читается как поломка, а не как правило.
    ///
    /// ═══ ПОЧЕМУ ПОЛОСА СТРОИТСЯ КОДОМ, А НЕ ЛЕЖИТ В СЦЕНЕ ═══
    ///
    /// Поправка П5 (docs/10 §8) считает сценовую работу главным риском: каждое окно —
    /// полдня расстановки и тихая поломка при ошибке. Отдельное <c>UIWindow</c> под
    /// всплывашку отпадает сразу (лишняя запись в реестре <c>UIService</c> = ещё один
    /// способ молча получить <c>null</c> из <c>Show&lt;T&gt;()</c>), но и три
    /// <c>[SerializeField]</c>-панели в трёх окнах — это три ручные привязки, без которых
    /// фича не работает, причём не работает МОЛЧА. Ровно так мета и оказалась невидимой
    /// в прошлый раз.
    ///
    /// Построенная кодом полоса не зависит от сцены вообще: она появляется в тот же
    /// момент, что и первый отказ, и на любом из трёх экранов. Когда придёт арт, её
    /// место займёт нарисованная панель — канал данных (<c>*ScreenModel.Refusal</c>)
    /// при этом не изменится.
    ///
    /// ═══ ДВЕ МЕЛОЧИ, БЕЗ КОТОРЫХ ОНА БЫ СЛОМАЛАСЬ ═══
    ///
    /// ШРИФТ берётся у соседнего текста окна, а не у TMP по умолчанию: дефолтный
    /// <c>LiberationSans SDF</c> собран без кириллицы, и русский отказ стал бы рядом
    /// квадратов (у проекта уже была такая история с кодировками, docs/08 §3).
    ///
    /// КЛИКИ полоса не перехватывает (<c>raycastTarget = false</c> у обоих графиков):
    /// она висит над карточками, и «кнопка перестала нажиматься» — худшее, чем может
    /// обернуться сообщение об ошибке.
    /// </summary>
    public abstract class UIMetaWindow : UIWindow
    {
        /// <summary>Высота полосы в канвас-единицах. Две строки текста при 40-м кегле.</summary>
        private const float BANNER_HEIGHT = 150f;

        private const float FONT_SIZE = 40f;

        private RectTransform _banner;
        private TextMeshProUGUI _bannerLabel;

        /// <summary>
        /// ПОКАЗАТЬ ИЛИ СПРЯТАТЬ ОТКАЗ. Зовётся из <c>Init(model)</c> каждого экрана
        /// значением <c>model.Refusal</c> — в том числе пустым, и это не формальность:
        /// пустая строка гасит прошлый отказ, поэтому сообщение живёт ровно до следующей
        /// перерисовки и не переживает ни успешное действие, ни переход на другой экран.
        ///
        /// Полоса создаётся ЛЕНИВО, при первом непустом отказе: у игрока, который ни разу
        /// не ошибся, в иерархии не появится ничего.
        /// </summary>
        protected void SetRefusal(string text)
        {
            bool show = !string.IsNullOrEmpty(text);

            if (!show)
            {
                if (_banner != null)
                    _banner.gameObject.SetActive(false);
                return;
            }

            if (_banner == null)
                BuildBanner();

            _bannerLabel.text = text;
            _banner.gameObject.SetActive(true);

            // Полоса обязана быть выше карточек, а они добавляются пулом уже после
            // первого показа окна — значит «последний ребёнок» надо подтверждать
            // каждый раз, а не один раз при создании.
            _banner.SetAsLastSibling();
        }

        private void BuildBanner()
        {
            // Шрифт — до создания собственного текста: иначе нашли бы сами себя.
            TMP_FontAsset font = BorrowFont();

            GameObject bannerObject = new GameObject("RefusalBanner", typeof(RectTransform), typeof(Image));
            _banner = bannerObject.GetComponent<RectTransform>();
            _banner.SetParent(transform, false);

            // Растянута по ширине окна, прижата к низу, с полями макета (docs/12 §3.2).
            _banner.anchorMin = new Vector2(0f, 0f);
            _banner.anchorMax = new Vector2(1f, 0f);
            _banner.pivot = new Vector2(0.5f, 0f);
            _banner.offsetMin = new Vector2(UILayout.SAFE_SIDE, UILayout.SAFE_BOTTOM);
            _banner.offsetMax = new Vector2(-UILayout.SAFE_SIDE, UILayout.SAFE_BOTTOM + BANNER_HEIGHT);

            Image background = bannerObject.GetComponent<Image>();
            background.color = new Color(0.09f, 0.08f, 0.12f, 0.92f);
            background.raycastTarget = false;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(_banner, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(24f, 12f);
            labelRect.offsetMax = new Vector2(-24f, -12f);

            _bannerLabel = labelObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                _bannerLabel.font = font;

            _bannerLabel.fontSize = FONT_SIZE;
            _bannerLabel.alignment = TextAlignmentOptions.Center;
            _bannerLabel.color = new Color(1f, 0.92f, 0.78f);
            _bannerLabel.textWrappingMode = TextWrappingModes.Normal;
            _bannerLabel.overflowMode = TextOverflowModes.Truncate;
            _bannerLabel.raycastTarget = false;
        }

        /// <summary>
        /// Взять шрифт у любого текста этого же окна. Он заведомо умеет кириллицу —
        /// иначе окно было бы нечитаемым и без нас.
        /// </summary>
        private TMP_FontAsset BorrowFont()
        {
            TextMeshProUGUI neighbour = GetComponentInChildren<TextMeshProUGUI>(true);
            return neighbour == null ? null : neighbour.font;
        }
    }
}
