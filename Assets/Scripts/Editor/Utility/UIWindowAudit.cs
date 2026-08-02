using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.UI;
using Utility.UI;
using Object = UnityEngine.Object;

namespace CatZombii.EditorTools
{
    /// <summary>
    /// ПРОВЕРКА РЕЕСТРА ОКОН И ЗАКОНА ВЁРСТКИ. Одна кнопка, один отчёт в консоль —
    /// как и <see cref="LocalizationAudit"/>, и ровно по той же причине.
    ///
    /// Зачем она существует. <c>UIService.InitWindows()</c> собирает окна через
    /// <c>FindObjectsByType&lt;UIWindow&gt;(FindObjectsInactive.Exclude)</c>, а
    /// <c>Show&lt;T&gt;()</c> при промахе делает <c>window?.Show()</c> и молча
    /// возвращает <c>null</c>. Значит выключенное окно, продублированное окно и
    /// окно, забытое в другой сцене, — всё это НЕ падает. Игра просто не открывает
    /// экран, а разработчик ищет причину в контроллерах. Проверка переводит эти три
    /// ошибки расстановки из «тихо сломалось» в «красная строка за секунду»
    /// (docs/12 §3.5, шаг М0; поправка П5 из docs/10 §8).
    ///
    /// Что проверяется:
    ///  1. Реестр: каждое окно активно, типы не дублируются, ни один тип не потерян.
    ///  2. Неоднозначность <c>Get&lt;T&gt;()</c>: поиск идёт по <c>x is T</c>, поэтому
    ///     запрос базового типа отдаст первое попавшееся окно-наследник.
    ///  3. <c>FlyerLayer</c>: есть, лежит последним ребёнком корневого канваса,
    ///     не является <c>UIWindow</c> и не перехватывает клики.
    ///  4. Скейлер: он ровно один, стоит на корневом канвасе, режим Expand,
    ///     референс 1080×1920 (docs/12 §3.1, §9).
    ///  5. Закон тапа: минимальная сторона интерактивного объекта — 150 канвас-единиц
    ///     (<see cref="UILayout.MIN_TAP_SIDE"/>, docs/12 §3.2).
    ///
    /// Проверка ЧИТАЮЩАЯ: она ничего не меняет в сцене и не помечает её изменённой.
    /// </summary>
    public static class UIWindowAudit
    {
        private const string MENU_ROOT = "Tools/Окна/";

        [MenuItem(MENU_ROOT + "Проверить реестр окон %#w")]
        public static void Audit()
        {
            int errors = 0;
            int warnings = 0;

            UIWindow[] windows = Object.FindObjectsByType<UIWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (windows.Length == 0)
            {
                Debug.LogError("[Окна] В открытой сцене нет ни одного UIWindow. Открыта та сцена? " +
                               "В сборке участвует только Assets/Scenes/SampleScene.unity.");
                return;
            }

            Canvas rootCanvas = FindRootCanvas();

            errors += AuditRegistry(windows, ref warnings);
            errors += AuditFlyerLayer(rootCanvas, ref warnings);
            errors += AuditScaler(rootCanvas);
            errors += AuditTapSizes(windows, rootCanvas, ref warnings);

            string verdict = errors == 0
                ? $"[Окна] Проверка пройдена. Окон: {windows.Length}. Предупреждений: {warnings}."
                : $"[Окна] ПРОВЕРКА НЕ ПРОЙДЕНА: ошибок {errors}, предупреждений {warnings}. Смотри сообщения выше.";

            if (errors == 0)
                Debug.Log(verdict);
            else
                Debug.LogError(verdict);
        }

        /// <summary>
        /// РЕЕСТР. Три ошибки расстановки, каждая из которых сегодня не падает:
        /// окно выключено (не попадёт в реестр), окно продублировано
        /// (<c>Find</c> отдаст первое, второе будет мёртвым) и окно потеряно
        /// (<c>Show&lt;T&gt;()</c> вернёт null).
        /// </summary>
        private static int AuditRegistry(UIWindow[] windows, ref int warnings)
        {
            int errors = 0;

            foreach (UIWindow window in windows)
            {
                if (!window.gameObject.activeInHierarchy)
                {
                    Debug.LogError($"[Окна] ОКНО ВЫКЛЮЧЕНО: «{Path(window.transform)}» ({window.GetType().Name}). " +
                                   "UIService ищет окна через FindObjectsInactive.Exclude — выключенное окно " +
                                   "в реестр не попадёт, а Show<T>() промолчит и вернёт null. Окна гасятся " +
                                   "Canvas.enabled, а не SetActive (docs/04, решение 4).", window);
                    errors++;
                }
            }

            // дубликаты типов
            foreach (IGrouping<Type, UIWindow> group in windows.GroupBy(w => w.GetType()))
            {
                if (group.Count() <= 1)
                    continue;

                Debug.LogError($"[Окна] ДУБЛИКАТ ТИПА {group.Key.Name}: {group.Count()} экземпляров — " +
                               string.Join(", ", group.Select(w => "«" + Path(w.transform) + "»")) +
                               ". UIService.Get<T>() отдаст первый попавшийся, остальные будут мёртвыми: " +
                               "контроллер подпишется на один экземпляр, а игрок нажмёт на другой.", group.First());
                errors++;
            }

            // потерянные типы: класс есть, экземпляра в сцене нет
            HashSet<Type> present = new(windows.Select(w => w.GetType()));
            foreach (Type type in TypeCache.GetTypesDerivedFrom<UIWindow>())
            {
                if (type.IsAbstract || present.Contains(type))
                    continue;

                Debug.LogError($"[Окна] ТИП ПОТЕРЯН: {type.Name} не представлен в сцене ни одним объектом. " +
                               "Show<" + type.Name + ">() вернёт null молча, и экран просто не откроется.");
                errors++;
            }

            // неоднозначность: Get<T> ищет по «x is T», значит базовый тип поймает наследника
            foreach (UIWindow window in windows)
            {
                Type type = window.GetType();
                UIWindow[] alsoMatching = windows.Where(w => w != window && type.IsInstanceOfType(w)).ToArray();
                if (alsoMatching.Length == 0)
                    continue;

                Debug.LogWarning($"[Окна] НЕОДНОЗНАЧНЫЙ ЗАПРОС: Get<{type.Name}>() совпадёт также с " +
                                 string.Join(", ", alsoMatching.Select(w => w.GetType().Name)) +
                                 " — поиск идёт по «x is T» (UIService.cs), а не по точному типу.", window);
                warnings++;
            }

            return errors;
        }

        /// <summary>
        /// FLYER LAYER — общий слой летящих объектов (docs/12 §6, «Мины при оживлении»).
        /// Он обязан быть ПОСЛЕДНИМ ребёнком корневого канваса (иначе летящее уедет под
        /// окна) и обязан НЕ быть <c>UIWindow</c>: попав в реестр, он был бы погашен
        /// первым же <c>HideAll()</c> из <c>GameManager.Start</c>.
        /// </summary>
        private static int AuditFlyerLayer(Canvas rootCanvas, ref int warnings)
        {
            if (rootCanvas == null)
                return 0;

            Transform layer = null;
            foreach (Transform child in rootCanvas.transform)
            {
                if (child.name != UILayout.FLYER_LAYER_NAME)
                    continue;
                layer = child;
                break;
            }

            if (layer == null)
            {
                Debug.LogError($"[Окна] НЕТ СЛОЯ «{UILayout.FLYER_LAYER_NAME}» среди прямых детей корневого канваса. " +
                               "Без него летящие объекты между окнами (шарик, иконка элемента, цифра урона) " +
                               "негде держать: окна — вложенные канвасы, объект из одного в другой не перелетает.",
                               rootCanvas);
                return 1;
            }

            int errors = 0;

            if (layer.GetComponent<UIWindow>() != null)
            {
                Debug.LogError($"[Окна] «{UILayout.FLYER_LAYER_NAME}» — наследник UIWindow. Так нельзя: он попадёт " +
                               "в реестр UIService и будет погашен первым же HideAll() (GameManager.Start).", layer);
                errors++;
            }

            if (layer.GetSiblingIndex() != rootCanvas.transform.childCount - 1)
            {
                Debug.LogError($"[Окна] «{UILayout.FLYER_LAYER_NAME}» не последний ребёнок канваса " +
                               $"(индекс {layer.GetSiblingIndex()} из {rootCanvas.transform.childCount - 1}). " +
                               "Летящее будет уезжать под окна.", layer);
                errors++;
            }

            if (!layer.gameObject.activeInHierarchy)
            {
                Debug.LogError($"[Окна] «{UILayout.FLYER_LAYER_NAME}» выключен.", layer);
                errors++;
            }

            Graphic graphic = layer.GetComponent<Graphic>();
            if (graphic != null && graphic.raycastTarget)
            {
                Debug.LogWarning($"[Окна] «{UILayout.FLYER_LAYER_NAME}» перехватывает клики (raycastTarget). " +
                                 "Слой лежит поверх всего — он съест нажатия по колбам и котлу.", layer);
                warnings++;
            }

            return errors;
        }

        /// <summary>
        /// СКЕЙЛЕР. Он один на весь проект и стоит на корневом канвасе: второй скейлер
        /// разъедется с первым при первой же смене ориентации (docs/12 §9).
        /// </summary>
        private static int AuditScaler(Canvas rootCanvas)
        {
            CanvasScaler[] scalers = Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (scalers.Length == 0)
            {
                Debug.LogError("[Вёрстка] В сцене нет ни одного CanvasScaler.");
                return 1;
            }

            int errors = 0;

            if (scalers.Length > 1)
            {
                Debug.LogError($"[Вёрстка] СКЕЙЛЕРОВ {scalers.Length}, а должен быть ОДИН на корневом канвасе. " +
                               "Все окна — вложенные канвасы одного корня; второй скейлер разъедется " +
                               "с первым при смене ориентации (docs/12 §9): " +
                               string.Join(", ", scalers.Select(s => "«" + Path(s.transform) + "»")), scalers[0]);
                errors++;
            }

            CanvasScaler scaler = scalers[0];

            if (rootCanvas != null && scaler.gameObject != rootCanvas.gameObject)
            {
                Debug.LogError($"[Вёрстка] CanvasScaler висит не на корневом канвасе, а на «{Path(scaler.transform)}».", scaler);
                errors++;
            }

            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                Debug.LogError($"[Вёрстка] Режим масштабирования — {scaler.uiScaleMode}, нужен ScaleWithScreenSize.", scaler);
                errors++;
            }

            if (scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand)
            {
                Debug.LogError($"[Вёрстка] ScreenMatchMode = {scaler.screenMatchMode}, нужен Expand. " +
                               "Только при нём scaleFactor = min(W/refW, H/refH), то есть контент " +
                               "никогда не обрезается, а запас уходит по одной оси (docs/12 §3.1).", scaler);
                errors++;
            }

            Vector2 reference = scaler.referenceResolution;
            bool isPortraitReference = Mathf.Approximately(reference.x, UILayout.REFERENCE_WIDTH_PORTRAIT) &&
                                       Mathf.Approximately(reference.y, UILayout.REFERENCE_HEIGHT_PORTRAIT);
            bool isLandscapeReference = Mathf.Approximately(reference.x, UILayout.REFERENCE_WIDTH_LANDSCAPE) &&
                                        Mathf.Approximately(reference.y, UILayout.REFERENCE_HEIGHT_LANDSCAPE);

            if (!isPortraitReference && !isLandscapeReference)
            {
                Debug.LogError($"[Вёрстка] Референс скейлера {reference.x}×{reference.y}. Допустимы только " +
                               $"{UILayout.REFERENCE_WIDTH_PORTRAIT}×{UILayout.REFERENCE_HEIGHT_PORTRAIT} (портрет) и " +
                               $"{UILayout.REFERENCE_WIDTH_LANDSCAPE}×{UILayout.REFERENCE_HEIGHT_LANDSCAPE} (ландшафт, М2). " +
                               "Именно из этого числа выведен закон тапа в 150 единиц (docs/12 §3.2).", scaler);
                errors++;
            }

            return errors;
        }

        /// <summary>
        /// ЗАКОН ТАПА: 150 канвас-единиц по минимальной стороне
        /// (<see cref="UILayout.MIN_TAP_SIDE"/>, docs/12 §3.2).
        ///
        /// Меряем в канвас-единицах, а не в пикселях: пиксели зависят от устройства,
        /// единицы — нет, а множитель между ними задан скейлером один раз на проект.
        ///
        /// Растянутые прямоугольники зависят от размера канваса, то есть от текущего
        /// разрешения Game View, — поэтому в шапке отчёта печатается, при каком размере
        /// канваса сделан замер. Ползунки и скроллбары исключены: их таскают, а не тапают,
        /// и 48 px к ним не применимы.
        /// </summary>
        private static int AuditTapSizes(UIWindow[] windows, Canvas rootCanvas, ref int warnings)
        {
            if (rootCanvas == null)
                return 0;

            RectTransform canvasRect = rootCanvas.transform as RectTransform;
            Vector2 canvasSize = canvasRect != null ? canvasRect.rect.size : Vector2.zero;
            Debug.Log($"[Вёрстка] Замер тапов сделан при размере канваса {canvasSize.x:0}×{canvasSize.y:0} ед. " +
                      $"(Game View {Screen.width}×{Screen.height}). Растянутые прямоугольники зависят от него — " +
                      "для честной проверки портрета поставь в Game View портретное разрешение, например 1080×1920.");

            int errors = 0;
            int tight = 0;

            foreach (UIWindow window in windows)
            {
                foreach (Selectable selectable in window.GetComponentsInChildren<Selectable>(true))
                {
                    if (!selectable.gameObject.activeInHierarchy)
                        continue;
                    if (selectable is Slider || selectable is Scrollbar)
                        continue;

                    RectTransform rect = selectable.transform as RectTransform;
                    if (rect == null)
                        continue;

                    Vector2 size = SizeInCanvasUnits(rect, rootCanvas.transform);
                    float minSide = Mathf.Min(size.x, size.y);

                    if (minSide + 0.5f < UILayout.MIN_TAP_SIDE)
                    {
                        Debug.LogError($"[Вёрстка] МАЛЫЙ ТАП: «{Path(rect)}» — {size.x:0}×{size.y:0} ед., " +
                                       $"минимальная сторона {minSide:0} < {UILayout.MIN_TAP_SIDE}. " +
                                       "Это меньше 48 CSS-px на целевом экране: аудитория играет одной рукой " +
                                       "в мобильном браузере (docs/09, docs/12 §3.2).", selectable);
                        errors++;
                    }
                    else if (minSide + 0.5f < UILayout.COMFORT_TAP_SIDE)
                    {
                        tight++;
                    }
                }
            }

            if (tight > 0)
            {
                Debug.Log($"[Вёрстка] На пороге: {tight} интерактивных объектов между {UILayout.MIN_TAP_SIDE} и " +
                          $"{UILayout.COMFORT_TAP_SIDE} ед. Это не ошибка — комфортный размер это цель, а не порог.");
            }

            return errors;
        }

        /// <summary>
        /// Размер прямоугольника в единицах КОРНЕВОГО канваса: собственный размер,
        /// помноженный на все локальные масштабы по дороге до корня. Через
        /// <c>lossyScale</c> считать нельзя — в него входит и масштаб самого канваса,
        /// то есть перевод в пиксели устройства, а мы меряем именно единицы.
        /// </summary>
        private static Vector2 SizeInCanvasUnits(RectTransform rect, Transform canvasRoot)
        {
            Vector2 size = rect.rect.size;
            Transform current = rect;
            int guard = 0;

            while (current != null && current != canvasRoot && guard++ < 128)
            {
                Vector3 scale = current.localScale;
                size.x *= Mathf.Abs(scale.x);
                size.y *= Mathf.Abs(scale.y);
                current = current.parent;
            }

            return size;
        }

        private static Canvas FindRootCanvas()
        {
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.isRootCanvas)
                    return canvas;
            }

            Debug.LogError("[Окна] В сцене нет корневого канваса.");
            return null;
        }

        private static string Path(Transform transform)
        {
            string result = transform.name;
            Transform current = transform;
            while (current.parent != null)
            {
                current = current.parent;
                result = current.name + "/" + result;
            }
            return result;
        }
    }
}
