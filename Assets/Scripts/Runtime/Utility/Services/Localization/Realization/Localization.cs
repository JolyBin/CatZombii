using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utility.Services.Localization
{
    /// <summary>
    /// Статический фасад над <see cref="ILocalizationService"/>.
    ///
    /// Почему статика, если в проекте «логика — POCO, связь через конструкторы».
    /// Строки нужны ДВУМ разным мирам: контроллерам (им конструктор передать можно)
    /// и компонентам на сцене (<see cref="LocalizedText"/>, <c>UIWindow</c>) — а этим
    /// конструктор не передать в принципе, их создаёт Unity. Прецедент в проекте уже
    /// есть — <c>UIService.Instance</c>. Вместо второго синглтона-MonoBehaviour здесь
    /// тонкий фасад: за ним обычный POCO, который можно подменить
    /// (<see cref="Install"/>) в тесте, в редакторной проверке или на экране настроек.
    ///
    /// Ленивая самоинициализация нужна из-за порядка Unity: <c>Awake</c> компонентов
    /// сцены случается раньше <c>GameManager.Start</c>, и текст обязан ставиться
    /// даже если явной установки не было.
    /// </summary>
    public static class Localization
    {
        private static ILocalizationService _service;

        public static ILocalizationService Service
        {
            get
            {
                if (_service == null)
                    Install(CreateDefault());
                return _service;
            }
        }

        /// <summary>Язык сменился — сцене надо перечитать строки. Переживает подмену сервиса.</summary>
        public static event Action OnLanguageChanged;

        public static GameLanguage Language => Service.Language;

        public static string Get(string key) => Service.Get(key);

        public static string Get(string key, params object[] args) => Service.Get(key, args);

        public static bool HasKey(string key) => Service.HasKey(key);

        public static void SetLanguage(GameLanguage language) => Service.SetLanguage(language);

        /// <summary>
        /// Подменить реализацию. Нужна редакторным проверкам и будущему экрану настроек;
        /// игровой код её не зовёт.
        /// </summary>
        public static void Install(ILocalizationService service)
        {
            if (_service != null)
                _service.OnLanguageChanged -= RaiseLanguageChanged;

            _service = service;

            if (_service != null)
                _service.OnLanguageChanged += RaiseLanguageChanged;

            RaiseLanguageChanged();
        }

        /// <summary>
        /// Сборка сервиса по умолчанию. ЗДЕСЬ И ТОЛЬКО ЗДЕСЬ задан приоритет источников:
        /// выбор игрока → площадка → система → фолбэк на русский. Порядок — правило
        /// продукта, поэтому он записан одной видимой строкой, а не размазан по классам.
        /// </summary>
        private static ILocalizationService CreateDefault()
        {
            LocalizationTable table = Resources.Load<LocalizationTable>(LocalizationTable.RESOURCES_PATH);
            List<ILanguageSource> sources = new()
            {
                new PlayerChoiceLanguageSource(),
                new PluginYGLanguageSource(),
                new SystemLanguageSource(),
            };
            return new LocalizationService(table, sources);
        }

        private static void RaiseLanguageChanged() => OnLanguageChanged?.Invoke();

#if UNITY_EDITOR
        /// <summary>
        /// Статика переживает выход из Play Mode при выключенном Domain Reload,
        /// а вместе с ней — устаревшая таблица и подписки прошлого запуска.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsForEditor()
        {
            if (_service != null)
                _service.OnLanguageChanged -= RaiseLanguageChanged;
            _service = null;
            OnLanguageChanged = null;
        }
#endif
    }
}
