using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utility.Services.Localization
{
    /// <summary>
    /// Реализация <see cref="ILocalizationService"/> — обычный C#-класс (POCO),
    /// как и вся логика проекта: <see cref="MonoBehaviour"/> здесь не нужен.
    /// Точка доступа для компонентов сцены — статический фасад <see cref="Localization"/>.
    /// </summary>
    public class LocalizationService : ILocalizationService
    {
        /// <summary>
        /// Чем отмечается пропавший ключ. Скобки выбраны так, чтобы маркер было видно
        /// и в игре, и в скриншоте, и в логе: <c>#home.play#</c> ни на что не похоже
        /// и не читается как текст.
        /// </summary>
        private const string MISSING_MARKER_WRAP = "#";

        /// <summary>Язык-источник: на него падает фолбэк, когда перевода нет.</summary>
        public const GameLanguage FALLBACK_LANGUAGE = GameLanguage.Russian;

        public GameLanguage Language { get; private set; }

        public event Action OnLanguageChanged;

        private readonly Dictionary<string, LocalizationEntry> _index;

        /// <summary>
        /// Ключи, о которых уже отругались. Без этого одна забытая строка в <c>Update</c>
        /// заливает консоль и прячет всё остальное — то есть проверка ломает сама себя.
        /// </summary>
        private readonly HashSet<string> _reportedKeys = new(StringComparer.Ordinal);

        public LocalizationService(LocalizationTable table, IReadOnlyList<ILanguageSource> languageSources = null)
        {
            _index = table != null
                ? table.BuildIndex()
                : new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);

            if (table == null)
                Debug.LogError("[Localization] Таблица строк не найдена — весь UI покажет маркеры вида #ключ#. " +
                               $"Ожидается ассет в Resources/{LocalizationTable.RESOURCES_PATH}.");

            Language = ResolveLanguage(languageSources);
        }

        public bool HasKey(string key) => !string.IsNullOrEmpty(key) && _index.ContainsKey(key);

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                ReportOnce(string.Empty, "[Localization] Запрошена строка по ПУСТОМУ ключу. " +
                                         "Скорее всего у LocalizedText на сцене не заполнено поле «Ключ».");
                return MISSING_MARKER_WRAP + "empty-key" + MISSING_MARKER_WRAP;
            }

            if (!_index.TryGetValue(key, out LocalizationEntry entry))
            {
                ReportOnce(key, $"[Localization] НЕТ КЛЮЧА «{key}». Добавьте строку в таблицу " +
                                $"Resources/{LocalizationTable.RESOURCES_PATH} (рецепт — docs/05 «Добавить строку»).");
                return MISSING_MARKER_WRAP + key + MISSING_MARKER_WRAP;
            }

            string value = entry.ForLanguage(Language);
            if (!string.IsNullOrEmpty(value))
                return value;

            // Перевода нет — это ШТАТНО, пока английский заведён пустым заделом (docs/09, фаза 2).
            // Отдаём язык-источник и молчим: ругаться на каждую непереведённую строку значило бы
            // залить лог целиком и утопить в нём настоящие пропажи ключей.
            string fallback = entry.ForLanguage(FALLBACK_LANGUAGE);
            if (!string.IsNullOrEmpty(fallback))
                return fallback;

            ReportOnce(key, $"[Localization] Ключ «{key}» есть, но ПУСТ на всех языках. " +
                            "Пустая строка в UI — это дефект, который находит игрок, а не разработчик.");
            return MISSING_MARKER_WRAP + key + MISSING_MARKER_WRAP;
        }

        public string Get(string key, params object[] args)
        {
            string template = Get(key);
            if (args == null || args.Length == 0)
                return template;

            try
            {
                return string.Format(template, args);
            }
            catch (FormatException exception)
            {
                // Кривой шаблон не имеет права ронять окно: игрок увидит шаблон,
                // разработчик — ошибку с именем ключа.
                Debug.LogError($"[Localization] Ключ «{key}»: шаблон «{template}» не принимает {args.Length} арг. " +
                               $"({exception.Message})");
                return template;
            }
        }

        public void SetLanguage(GameLanguage language)
        {
            if (Language == language)
                return;
            Language = language;
            OnLanguageChanged?.Invoke();
        }

        /// <summary>
        /// Опрос источников ПО ПОРЯДКУ, первый ответивший выигрывает. Порядок задаёт
        /// тот, кто собирает сервис (см. <see cref="Localization"/>), а не этот класс:
        /// «выбор игрока важнее платформы, платформа важнее системы» — это правило
        /// продукта, и оно обязано быть видно в одном месте, а не размазано по источникам.
        /// </summary>
        private GameLanguage ResolveLanguage(IReadOnlyList<ILanguageSource> sources)
        {
            if (sources == null)
                return FALLBACK_LANGUAGE;

            foreach (ILanguageSource source in sources)
            {
                if (source == null)
                    continue;
                if (!source.TryGetLanguage(out GameLanguage language))
                    continue;

                Debug.Log($"[Localization] Язык: {language} (источник: {source.Name}).");
                return language;
            }

            Debug.Log($"[Localization] Язык: {FALLBACK_LANGUAGE} (ни один источник не ответил — фолбэк).");
            return FALLBACK_LANGUAGE;
        }

        private void ReportOnce(string key, string message)
        {
            if (!_reportedKeys.Add(key))
                return;
            Debug.LogError(message);
        }
    }
}
