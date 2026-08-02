using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.UI;

namespace Utility.Services.UI
{
    /// <summary>
    /// ПОЛОСА СООБЩЕНИЯ НАД ЛЮБЫМ ОКНОМ — «почему это нельзя», сказанное игроку,
    /// а не консоли.
    ///
    /// ═══ ЗАЧЕМ ═══
    ///
    /// Правило проекта: молчаливый отказ читается как поломка, а не как правило.
    /// Кнопка, которая не нажимается без объяснения, — это баг в глазах игрока,
    /// даже когда это дизайн.
    ///
    /// ═══ ПОЧЕМУ КОДОМ, А НЕ ОБЪЕКТОМ В СЦЕНЕ ═══
    ///
    /// Ровно по тем же причинам, по которым так же сделана полоса отказов на экранах
    /// меты (<c>Meta.UI.UIMetaWindow.SetRefusal</c>): поправка П5 (docs/10 §8) называет
    /// сценовую работу главным риском плана, отдельное <c>UIWindow</c> под всплывашку —
    /// это лишняя запись в реестре <c>UIService</c> и ещё один способ молча получить
    /// <c>null</c> из <c>Show&lt;T&gt;()</c>, а <c>[SerializeField]</c>-панель в каждом
    /// окне — ручная привязка, без которой фича не работает МОЛЧА.
    ///
    /// ⚠️ Этот класс — вынесенная наружу версия того же приёма, и он существует только
    /// потому, что окна меты (<c>Meta/UI</c>) в этой правке трогать было нельзя. Когда
    /// <c>UIMetaWindow.SetRefusal</c> будет переписан на него, в проекте останется одна
    /// полоса вместо двух одинаковых. До тех пор — две, и это известный долг.
    ///
    /// ═══ ДВЕ МЕЛОЧИ, БЕЗ КОТОРЫХ ОНА БЫ СЛОМАЛАСЬ ═══
    ///
    /// ШРИФТ берётся у соседнего текста окна, а не у TMP по умолчанию: дефолтный
    /// <c>LiberationSans SDF</c> собран без кириллицы, и русское сообщение стало бы
    /// рядом квадратов (у проекта уже была такая история, docs/08 §3).
    ///
    /// КЛИКИ полоса не перехватывает (<c>raycastTarget = false</c> у обоих графиков):
    /// она висит над карточками, и «кнопка перестала нажиматься» — худшее, чем может
    /// обернуться сообщение об ошибке.
    /// </summary>
    public sealed class UINotice : IAction
    {
        /// <summary>Высота полосы в канвас-единицах. Две строки текста при 40-м кегле.</summary>
        private const float BANNER_HEIGHT = 150f;

        private const float FONT_SIZE = 40f;

        private RectTransform _banner;
        private TextMeshProUGUI _label;

        /// <summary>Окно, над которым полоса висит сейчас. Сменилось — полосу пересобираем.</summary>
        private UIWindow _host;

        /// <summary>
        /// ПОКАЗАТЬ ИЛИ ПОГАСИТЬ. Пустой <paramref name="text"/> гасит — и это не
        /// формальность: сообщение обязано жить ровно до следующего осмысленного
        /// действия, иначе «герой ещё в работе» переезжает на экран, где героев нет.
        ///
        /// Полоса создаётся ЛЕНИВО, при первом непустом сообщении: у игрока, который
        /// ни разу не наткнулся на отказ, в иерархии не появится ничего.
        /// </summary>
        public void Show(UIWindow window, string text)
        {
            if (window == null || string.IsNullOrEmpty(text))
            {
                Clear();
                return;
            }

            if (_host != window)
            {
                DestroyBanner();
                _host = window;
            }

            if (_banner == null)
                Build();

            _label.text = text;
            _banner.gameObject.SetActive(true);

            // Полоса обязана быть выше карточек, а они могут добавляться пулом уже после
            // первого показа окна — значит «последний ребёнок» надо подтверждать каждый
            // раз, а не один раз при создании.
            _banner.SetAsLastSibling();
        }

        /// <summary>Погасить, не разбирая: следующее сообщение переиспользует ту же полосу.</summary>
        public void Clear()
        {
            if (_banner != null)
                _banner.gameObject.SetActive(false);
        }

        /// <summary>
        /// Разобрать совсем. По правилу проекта зовётся владельцем в его <c>Exit()</c>:
        /// полоса — созданный кодом объект на чужом окне, и без этого она пережила бы
        /// своего хозяина (docs/04, «Освобождение ресурсов»).
        /// </summary>
        public void ClearAction() => DestroyBanner();

        private void DestroyBanner()
        {
            if (_banner != null)
                Object.Destroy(_banner.gameObject);

            _banner = null;
            _label = null;
            _host = null;
        }

        private void Build()
        {
            // Шрифт — ДО создания собственного текста: иначе нашли бы сами себя.
            TMP_FontAsset font = BorrowFont();

            GameObject bannerObject = new GameObject("NoticeBanner", typeof(RectTransform), typeof(Image));
            _banner = bannerObject.GetComponent<RectTransform>();
            _banner.SetParent(_host.transform, false);

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

            _label = labelObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                _label.font = font;

            _label.fontSize = FONT_SIZE;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = new Color(1f, 0.92f, 0.78f);
            _label.textWrappingMode = TextWrappingModes.Normal;
            _label.overflowMode = TextOverflowModes.Truncate;
            _label.raycastTarget = false;
        }

        /// <summary>
        /// Взять шрифт у любого текста этого же окна. Он заведомо умеет кириллицу —
        /// иначе окно было бы нечитаемым и без нас.
        /// </summary>
        private TMP_FontAsset BorrowFont()
        {
            TextMeshProUGUI neighbour = _host.GetComponentInChildren<TextMeshProUGUI>(true);
            return neighbour == null ? null : neighbour.font;
        }
    }
}
