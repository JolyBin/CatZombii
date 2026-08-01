using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utility.Services.Localization
{
    /// <summary>
    /// Одна строка таблицы: ключ и по колонке на язык.
    ///
    /// Колонки, а не файл на язык, — сознательно. При файле на язык вопрос
    /// «что ещё не переведено» решается диффом двух файлов; при колонках пустая
    /// ячейка видна глазом прямо рядом с заполненной, и редакторная проверка
    /// (<c>Tools → Локализация</c>) считает её за секунду.
    /// </summary>
    [Serializable]
    public class LocalizationEntry
    {
        [Tooltip("Ключ. Соглашение: <экран>.<элемент>, нижний регистр, слова через _. " +
                 "Например home.play, battle.wave, spell.power_attack.")]
        public string Key;

        [Tooltip("РУССКИЙ — язык-источник. Заполняется всегда: на него падает фолбэк.")]
        [TextArea(1, 4)] public string Russian;

        [Tooltip("АНГЛИЙСКИЙ — задел под фазу 2 (docs/09). Пустая ячейка не ошибка: " +
                 "строка отдаётся по-русски, а проверка сообщает, сколько ещё не переведено.")]
        [TextArea(1, 4)] public string English;

        public string ForLanguage(GameLanguage language) => language switch
        {
            GameLanguage.English => English,
            _ => Russian,
        };
    }

    /// <summary>
    /// ХРАНИЛИЩЕ СТРОК — свой <see cref="ScriptableObject"/> в <c>Resources/</c>.
    ///
    /// Почему так, а не CSV/JSON и не Unity Localization Package:
    ///
    /// 1. Так устроены ВСЕ конфиги проекта (<c>BattleConfig</c>, <c>UnitConfig</c>,
    ///    <c>Book</c>, <c>*SpellConfig</c>) — новая сущность не заводит второго
    ///    способа хранить данные и второго места, куда смотреть.
    /// 2. У проекта ЕСТЬ история порчи кодировок: три файла лежали в CP1251, и русские
    ///    строки превращались в мусор прямо в игре (docs/08 §3). Внешний текстовый файл —
    ///    ровно тот носитель, который и портится; <c>.asset</c> пишет сам Unity,
    ///    всегда в UTF-8, и испортить его редактором нельзя.
    /// 3. <c>Resources.Load</c> работает на WebGL без Addressables и без загрузчика.
    /// 4. YAML диффится в git построчно, а инспектор даёт редактирование без инструмента.
    ///
    /// Цена, которую надо знать: таблица грузится целиком (при десятках строк — ничто,
    /// при тысячах понадобится разбиение), и параллельная правка одного ассета двумя
    /// людьми даёт конфликт слияния. Оба порога далеко.
    /// </summary>
    [CreateAssetMenu(fileName = "Localization Table", menuName = "Localization/Create Localization Table")]
    public class LocalizationTable : ScriptableObject
    {
        /// <summary>Путь внутри <c>Resources/</c>. Меняешь путь — меняй здесь, второго места нет.</summary>
        public const string RESOURCES_PATH = "Localization/Localization Table";

        [SerializeField] private LocalizationEntry[] _entries = Array.Empty<LocalizationEntry>();

        public IReadOnlyList<LocalizationEntry> Entries => _entries;

        /// <summary>
        /// Индекс ключ → строка. Дубликат ключа — ошибка данных, а не «последний выигрывает»:
        /// молча победивший дубль означает, что кто-то правит строку, которой игрок не видит.
        /// </summary>
        public Dictionary<string, LocalizationEntry> BuildIndex()
        {
            Dictionary<string, LocalizationEntry> index = new(_entries.Length, StringComparer.Ordinal);
            foreach (LocalizationEntry entry in _entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                {
                    Debug.LogError($"[Localization] В таблице «{name}» есть строка без ключа — она недостижима.", this);
                    continue;
                }
                if (index.ContainsKey(entry.Key))
                {
                    Debug.LogError($"[Localization] Дубликат ключа «{entry.Key}» в таблице «{name}». " +
                                   "Вторая строка игнорируется — уберите её, иначе будете править невидимую.", this);
                    continue;
                }
                index.Add(entry.Key, entry);
            }
            return index;
        }

#if UNITY_EDITOR
        /// <summary>Редакторная запись — только для миграций и генераторов, в рантайме таблица неизменна.</summary>
        public void EditorSetEntries(LocalizationEntry[] entries) => _entries = entries;
#endif
    }
}
