namespace Utility.UI
{
    /// <summary>
    /// ЗАКОН ВЁРСТКИ, вынесенный в одно место (docs/12 §3.2).
    ///
    /// Числа здесь — не «настройки на вкус», а выведенные величины. Референс канваса
    /// 1080×1920 с режимом Expand даёт scaleFactor = min(W/1080, H/1920), то есть
    /// на худшем целевом экране (360×800) множитель 0,333. Отсюда:
    /// 48 CSS-px минимального тапа × (1 / 0,333) ≈ 144 канвас-единицы, округлено вверх
    /// до 150. Меняешь референс — пересчитывай <see cref="MIN_TAP_SIDE"/>, иначе
    /// весь UI молча уезжает под палец.
    ///
    /// Запасы SAFE_* — авторские поля макета (нотч, адресная строка, «домашняя полоска»,
    /// свайп-назад). Они НЕ заменяют системную безопасную зону устройства: её отдельно
    /// применяет <see cref="SafeAreaFitter"/>, и эти два запаса складываются.
    /// </summary>
    public static class UILayout
    {
        /// <summary>Референс канваса в портрете. Единственный на проект (docs/12 §9).</summary>
        public const float REFERENCE_WIDTH_PORTRAIT = 1080f;

        public const float REFERENCE_HEIGHT_PORTRAIT = 1920f;

        /// <summary>Референс канваса в ландшафте — понадобится М2 (LayoutSwitcher).</summary>
        public const float REFERENCE_WIDTH_LANDSCAPE = 1920f;

        public const float REFERENCE_HEIGHT_LANDSCAPE = 1080f;

        /// <summary>
        /// Минимальная сторона ЛЮБОГО интерактивного объекта в канвас-единицах.
        /// Это ≥ 48 CSS-px на худшем целевом экране (docs/12 §3.2).
        /// </summary>
        public const float MIN_TAP_SIDE = 150f;

        /// <summary>Комфортная сторона — цель, а не порог. Ниже неё ошибкой не считаем.</summary>
        public const float COMFORT_TAP_SIDE = 180f;

        /// <summary>Верхний запас макета: нотч и адресная строка.</summary>
        public const float SAFE_TOP = 90f;

        /// <summary>Нижний запас макета: панель браузера и «домашняя полоска».</summary>
        public const float SAFE_BOTTOM = 120f;

        /// <summary>Боковой запас макета: скругления экрана и свайп-назад.</summary>
        public const float SAFE_SIDE = 40f;

        /// <summary>
        /// Имя слоя летящих объектов в корне канваса (docs/12 §6, «Мины при оживлении»).
        /// Не <c>UIWindow</c> сознательно: попав в реестр <c>UIService</c>, он был бы
        /// погашен первым же <c>HideAll()</c>.
        /// </summary>
        public const string FLYER_LAYER_NAME = "FlyerLayer";

        /// <summary>Имена четырёх контейнеров боевого экрана (docs/12 §3.3).</summary>
        public const string CONTAINER_HUD = "Hud";

        public const string CONTAINER_FIELD = "Field";
        public const string CONTAINER_KITCHEN = "Kitchen";
        public const string CONTAINER_SHELF = "Shelf";
    }
}
