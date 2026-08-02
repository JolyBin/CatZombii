using UnityEngine;

namespace Utility.UI
{
    /// <summary>
    /// БЕЗОПАСНАЯ ЗОНА УСТРОЙСТВА. Ужимает свой <see cref="RectTransform"/> до
    /// <see cref="Screen.safeArea"/> — области, которую не режут нотч, скруглённые углы
    /// и системные панели.
    ///
    /// Зачем отдельным компонентом, если в макете уже есть запасы SAFE_TOP/BOTTOM/SIDE
    /// (<see cref="UILayout"/>). Запасы макета — величины постоянные, они выбраны под
    /// «средний плохой» телефон. Реальный вырез разный на каждом устройстве и вдобавок
    /// меняется на лету: в мобильном браузере адресная строка уезжает при скролле, и
    /// safeArea пересчитывается прямо во время игры. Одно другое не заменяет, поэтому
    /// они СКЛАДЫВАЮТСЯ: этот компонент режет по железу, а контейнеры внутри него уже
    /// отступают на авторские поля.
    ///
    /// Работает якорями, а не отступами: якоря — доли родителя, поэтому результат
    /// не зависит ни от текущего разрешения, ни от <c>CanvasScaler</c>, и не ломается
    /// при смене ориентации.
    ///
    /// ⚠️ Вешать ТОЛЬКО на контейнер с интерфейсом. Фон, затемнение и любой другой
    /// полноэкранный арт обязаны остаться СНАРУЖИ: ужатый фон оставит непрокрашенную
    /// полосу ровно там, где вырез.
    ///
    /// Намеренно НЕ <c>[ExecuteAlways]</c>: в редакторе <c>Screen.safeArea</c> равен
    /// всему экрану Game View, то есть компонент писал бы в сцену мусор и помечал её
    /// изменённой на каждое открытие.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeAreaFitter : MonoBehaviour
    {
        [Tooltip("Резать по вырезу сверху. Выключается, если под верхним краем и так " +
                 "лежит только фон.")]
        [SerializeField] private bool _applyTop = true;

        [SerializeField] private bool _applyBottom = true;
        [SerializeField] private bool _applyLeft = true;
        [SerializeField] private bool _applyRight = true;

        private RectTransform _rect;
        private Rect _lastSafeArea = new Rect(0, 0, 0, 0);
        private Vector2Int _lastScreenSize = Vector2Int.zero;

        private void Awake() => _rect = GetComponent<RectTransform>();

        private void OnEnable() => Apply();

        /// <summary>
        /// Опрос, а не событие: единого кросс-платформенного события смены безопасной
        /// зоны нет, а в WebGL она меняется от жестов браузера, а не от поворота.
        /// Сравнение двух структур раз в кадр дешевле любой подписки.
        /// </summary>
        private void Update()
        {
            if (Screen.safeArea == _lastSafeArea &&
                Screen.width == _lastScreenSize.x &&
                Screen.height == _lastScreenSize.y)
                return;

            Apply();
        }

        public void Apply()
        {
            if (_rect == null)
                _rect = GetComponent<RectTransform>();

            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            if (screenWidth <= 0 || screenHeight <= 0)
                return;

            Rect safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(screenWidth, screenHeight);

            Vector2 min = new Vector2(safeArea.xMin / screenWidth, safeArea.yMin / screenHeight);
            Vector2 max = new Vector2(safeArea.xMax / screenWidth, safeArea.yMax / screenHeight);

            if (!_applyLeft) min.x = 0f;
            if (!_applyBottom) min.y = 0f;
            if (!_applyRight) max.x = 1f;
            if (!_applyTop) max.y = 1f;

            // Битый safeArea (ноль или инверсия) в WebGL встречается на ранних кадрах,
            // пока страница не отдала размеры. Ужать интерфейс в точку — хуже,
            // чем проигнорировать кадр.
            if (max.x - min.x <= 0f || max.y - min.y <= 0f)
                return;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
