using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using Utility.Services.Localization;
using Utility.Services.UI;

namespace CatZombii.EditorTools
{
    /// <summary>
    /// ПРОВЕРКА ПОЛНОТЫ ЛОКАЛИЗАЦИИ. Одна кнопка, один отчёт в консоль.
    ///
    /// Зачем она вообще. Правило «ни одной пользовательской строки литералом»
    /// не соблюдается само: забытая строка не ломает компиляцию, не роняет игру
    /// и не видна в диффе — она просто выходит в релиз на чужом языке. Проверка
    /// делает забытую строку НАХОДИМОЙ за одну секунду, и это единственное,
    /// ради чего она существует. Поэтому она намеренно простая: меню + Debug.Log
    /// со ссылками на объекты, без окна, без настроек и без своего формата отчёта.
    ///
    /// Что проверяется:
    ///  1. Сцена: каждый TMP внутри окна (<see cref="UIWindow"/>) обязан иметь
    ///     <see cref="LocalizedText"/>. Нет компонента и объект активен → ОШИБКА.
    ///     Нет компонента, но объект выключен → предупреждение (мёртвый UI:
    ///     либо подключить, либо удалить).
    ///  2. Ассеты: у каждого заклинания, юнита и книги ключ заполнен и найден в таблице.
    ///  3. Таблица: пустые русские ячейки (дырка в языке-источнике) и счётчик
    ///     непереведённого английского (задел фазы 2 — не ошибка, а мера долга).
    ///  4. Ключи-сироты: строка в таблице, на которую никто не ссылается.
    /// </summary>
    public static class LocalizationAudit
    {
        private const string MENU_ROOT = "Tools/Локализация/";

        [MenuItem(MENU_ROOT + "Проверить сцену и ассеты %#l")]
        public static void Audit()
        {
            LocalizationTable table = LoadTable();
            HashSet<string> tableKeys = table == null
                ? new HashSet<string>()
                : new HashSet<string>(table.Entries.Where(e => e != null && !string.IsNullOrWhiteSpace(e.Key)).Select(e => e.Key));
            HashSet<string> usedKeys = new();

            int errors = 0;
            int warnings = 0;

            CollectCodeKeys(usedKeys);
            CollectSceneKeys(usedKeys);
            errors += AuditScene(tableKeys, usedKeys, ref warnings);
            errors += AuditPrefabs(tableKeys, usedKeys, ref warnings);
            errors += AuditAssets(tableKeys, usedKeys);
            errors += AuditTable(table, usedKeys, ref warnings);

            string verdict = errors == 0
                ? $"[Локализация] Проверка пройдена. Предупреждений: {warnings}."
                : $"[Локализация] ПРОВЕРКА НЕ ПРОЙДЕНА: ошибок {errors}, предупреждений {warnings}. Смотри сообщения выше.";

            if (errors == 0)
                Debug.Log(verdict);
            else
                Debug.LogError(verdict);
        }

        /// <summary>
        /// Ключи, которые запрашивает КОД — читаются из <see cref="LocKeys"/> рефлексией.
        /// Без этого шага все три «кодовых» ключа попадали бы в список сирот на каждом
        /// запуске, и список сирот перестал бы читаться — а он единственный способ
        /// заметить строку, которую больше никто не показывает.
        /// </summary>
        private static void CollectCodeKeys(HashSet<string> usedKeys)
        {
            foreach (System.Reflection.FieldInfo field in typeof(LocKeys).GetFields(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (field.IsLiteral && field.FieldType == typeof(string) && field.GetRawConstantValue() is string key)
                    usedKeys.Add(key);
            }
        }

        /// <summary>
        /// Ключи со ВСЕЙ сцены, включая экраны вне <see cref="UIWindow"/> (сегодня это
        /// экран паузы — он собран китом, но к коду не подключён). Такой экран не обязан
        /// проходить проверку на литералы, но его ключи — не сироты.
        /// </summary>
        private static void CollectSceneKeys(HashSet<string> usedKeys)
        {
            foreach (LocalizedText localized in Object.FindObjectsByType<LocalizedText>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (localized.Source == LocalizedTextSource.Key && !string.IsNullOrWhiteSpace(localized.Key))
                    usedKeys.Add(localized.Key);
            }
        }

        /// <summary>
        /// Сцена. Ходим от ОКОН, а не от всех TMP подряд: текст вне окна игроку
        /// показать нечем — <c>UIService</c> оперирует только окнами.
        /// </summary>
        private static int AuditScene(HashSet<string> tableKeys, HashSet<string> usedKeys, ref int warnings)
        {
            int errors = 0;
            int placeholders = 0;

            UIWindow[] windows = Object.FindObjectsByType<UIWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (windows.Length == 0)
            {
                Debug.LogWarning("[Локализация] В открытой сцене нет ни одного UIWindow — проверять нечего. Открыта та сцена?");
                warnings++;
                return 0;
            }

            foreach (UIWindow window in windows)
            {
                foreach (TMP_Text text in window.GetComponentsInChildren<TMP_Text>(true))
                {
                    LocalizedText localized = text.GetComponent<LocalizedText>();

                    if (localized == null)
                    {
                        // Пустой TMP без компонента ничего игроку не врёт — он пуст.
                        if (string.IsNullOrWhiteSpace(text.text))
                            continue;

                        if (!text.gameObject.activeInHierarchy)
                        {
                            Debug.LogWarning($"[Локализация] МЁРТВЫЙ UI: «{Path(text.transform)}» выключен и содержит литерал " +
                                             $"«{Short(text.text)}». Либо подключить и локализовать, либо удалить.", text);
                            warnings++;
                            continue;
                        }

                        Debug.LogError($"[Локализация] ЛИТЕРАЛ В СЦЕНЕ: «{Path(text.transform)}» → «{Short(text.text)}». " +
                                       "Повесь LocalizedText и задай ключ (docs/05 «Добавить строку»).", text);
                        errors++;
                        continue;
                    }

                    switch (localized.Source)
                    {
                        case LocalizedTextSource.Key:
                            if (string.IsNullOrWhiteSpace(localized.Key))
                            {
                                Debug.LogError($"[Локализация] ПУСТОЙ КЛЮЧ у LocalizedText на «{Path(text.transform)}».", text);
                                errors++;
                                break;
                            }
                            usedKeys.Add(localized.Key);
                            if (!tableKeys.Contains(localized.Key))
                            {
                                Debug.LogError($"[Локализация] НЕТ В ТАБЛИЦЕ ключа «{localized.Key}» " +
                                               $"(объект «{Path(text.transform)}»).", text);
                                errors++;
                            }
                            break;

                        case LocalizedTextSource.Placeholder:
                            placeholders++;
                            break;
                    }
                }
            }

            if (placeholders > 0)
                Debug.Log($"[Локализация] Заглушек чужого UI-кита на сцене: {placeholders} — уйдут вместе с китом в фазе 0 (docs/09).");

            return errors;
        }

        /// <summary>
        /// ПРЕФАБЫ в <c>Resources/</c> — вторая половина сцены, и половина коварная:
        /// юниты боя и карточки создаются в рантайме, поэтому их текст не виден
        /// ни в одном осмотре сцены. Именно так в проекте выжил литерал «BOSS»
        /// на каждом зомби — нашёлся только прогоном игры.
        ///
        /// Здесь нет деления на активные и выключенные: у префаба активность ничего
        /// не значит, его включает тот, кто инстанцирует.
        /// </summary>
        private static int AuditPrefabs(HashSet<string> tableKeys, HashSet<string> usedKeys, ref int warnings)
        {
            int errors = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                // Префаб с потерянным скриптом Unity отказывается сохранять — повесить
                // на него LocalizedText физически нельзя, и вечная ошибка «литерал»
                // тут была бы не находкой, а шумом, который приучает не читать отчёт.
                if (HasMissingScript(prefab))
                {
                    Debug.LogWarning($"[Локализация] ПРОПУЩЕН префаб «{path}»: в нём потерян скрипт, " +
                                     "Unity не даст его сохранить. Почини ссылку или удали префаб — " +
                                     "до этого локализация в нём не проверяется.", prefab);
                    warnings++;
                    continue;
                }

                foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
                {
                    LocalizedText localized = text.GetComponent<LocalizedText>();

                    if (localized == null)
                    {
                        if (string.IsNullOrWhiteSpace(text.text))
                            continue;
                        Debug.LogError($"[Локализация] ЛИТЕРАЛ В ПРЕФАБЕ «{path}» → объект «{text.name}», текст «{Short(text.text)}». " +
                                       "Повесь LocalizedText.", prefab);
                        errors++;
                        continue;
                    }

                    if (localized.Source != LocalizedTextSource.Key)
                        continue;

                    if (string.IsNullOrWhiteSpace(localized.Key))
                    {
                        Debug.LogError($"[Локализация] ПУСТОЙ КЛЮЧ в префабе «{path}», объект «{text.name}».", prefab);
                        errors++;
                        continue;
                    }

                    usedKeys.Add(localized.Key);
                    if (!tableKeys.Contains(localized.Key))
                    {
                        Debug.LogError($"[Локализация] НЕТ В ТАБЛИЦЕ ключа «{localized.Key}» (префаб «{path}»).", prefab);
                        errors++;
                    }
                }
            }

            return errors;
        }

        /// <summary>Ассеты: ключи имён заклинаний, юнитов и героев.</summary>
        private static int AuditAssets(HashSet<string> tableKeys, HashSet<string> usedKeys)
        {
            int errors = 0;

            foreach (Core.Spells.BaseSpellConfig spell in LoadAll<Core.Spells.BaseSpellConfig>())
                errors += CheckAssetKey(spell, spell.NameKey, "имя заклинания", required: true, tableKeys, usedKeys);

            foreach (Core.Battle.UnitConfig unit in LoadAll<Core.Battle.UnitConfig>())
                // пустой ключ у юнита — законное «без подписи», см. UnitConfig.Name
                errors += CheckAssetKey(unit, unit.NameKey, "имя юнита", required: false, tableKeys, usedKeys);

            foreach (Core.Spells.Book book in LoadAll<Core.Spells.Book>())
            {
                errors += CheckAssetKey(book, book.NameHeroKey, "имя героя", required: true, tableKeys, usedKeys);
                errors += CheckAssetKey(book, book.ClassHeroKey, "класс героя", required: true, tableKeys, usedKeys);
            }

            return errors;
        }

        private static int CheckAssetKey(Object asset, string key, string what, bool required,
                                         HashSet<string> tableKeys, HashSet<string> usedKeys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                if (!required)
                    return 0;
                Debug.LogError($"[Локализация] ПУСТОЙ КЛЮЧ ({what}) в ассете «{AssetDatabase.GetAssetPath(asset)}».", asset);
                return 1;
            }

            usedKeys.Add(key);
            if (tableKeys.Contains(key))
                return 0;

            Debug.LogError($"[Локализация] НЕТ В ТАБЛИЦЕ ключа «{key}» ({what}, ассет " +
                           $"«{AssetDatabase.GetAssetPath(asset)}»).", asset);
            return 1;
        }

        /// <summary>Сама таблица: дырки в русском, долг по английскому, ключи-сироты.</summary>
        private static int AuditTable(LocalizationTable table, HashSet<string> usedKeys, ref int warnings)
        {
            if (table == null)
            {
                Debug.LogError($"[Локализация] Таблица не найдена: Resources/{LocalizationTable.RESOURCES_PATH}.");
                return 1;
            }

            int errors = 0;
            int untranslated = 0;
            List<string> orphans = new();

            foreach (LocalizationEntry entry in table.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                {
                    Debug.LogError("[Локализация] В таблице есть строка без ключа.", table);
                    errors++;
                    continue;
                }

                if (string.IsNullOrEmpty(entry.Russian))
                {
                    Debug.LogError($"[Локализация] ПУСТОЙ РУССКИЙ у ключа «{entry.Key}». " +
                                   "Русский — язык-источник, на него падает фолбэк: дырка здесь видна игроку.", table);
                    errors++;
                }

                if (string.IsNullOrEmpty(entry.English))
                    untranslated++;

                if (!usedKeys.Contains(entry.Key))
                    orphans.Add(entry.Key);
            }

            if (untranslated > 0)
                Debug.Log($"[Локализация] Английский: не переведено {untranslated} из {table.Entries.Count}. " +
                          "Это плановый долг фазы 2 (docs/09), не ошибка — строки уходят игроку по-русски.");

            if (orphans.Count > 0)
            {
                Debug.LogWarning($"[Локализация] КЛЮЧИ-СИРОТЫ ({orphans.Count}): на них не ссылается ни сцена, ни ассеты — " +
                                 $"{string.Join(", ", orphans)}. Проверка не видит ключи, которые код собирает динамически, " +
                                 "поэтому это предупреждение, а не ошибка.", table);
                warnings++;
            }

            return errors;
        }

        [MenuItem(MENU_ROOT + "Выделить таблицу строк")]
        public static void SelectTable()
        {
            LocalizationTable table = LoadTable();
            if (table == null)
            {
                Debug.LogError($"[Локализация] Таблица не найдена: Resources/{LocalizationTable.RESOURCES_PATH}.");
                return;
            }
            Selection.activeObject = table;
            EditorGUIUtility.PingObject(table);
        }

        private static LocalizationTable LoadTable()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(LocalizationTable));
            if (guids.Length == 0)
                return null;
            if (guids.Length > 1)
                Debug.LogWarning($"[Локализация] Таблиц строк найдено {guids.Length} — в игру попадёт только та, " +
                                 $"что лежит по пути Resources/{LocalizationTable.RESOURCES_PATH}.");
            return AssetDatabase.LoadAssetAtPath<LocalizationTable>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>
        /// Все ассеты типа T. Ищем по <c>t:ScriptableObject</c> и фильтруем в коде,
        /// а не через <c>t:BaseSpellConfig</c>: фильтр по имени АБСТРАКТНОГО базового
        /// класса ведёт себя по-разному в зависимости от состояния индекса поиска,
        /// а незамеченный ассет здесь означает незамеченную непереведённую строку.
        /// </summary>
        private static IEnumerable<T> LoadAll<T>() where T : Object
            => AssetDatabase.FindAssets("t:ScriptableObject")
                            .Select(AssetDatabase.GUIDToAssetPath)
                            .Where(path => path.StartsWith("Assets/"))
                            .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>)
                            .OfType<T>();

        private static bool HasMissingScript(GameObject root)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                if (component == null)
                    return true;
            return false;
        }

        private static string Short(string value)
        {
            string oneLine = value.Replace("\n", " ").Replace("\r", " ").Trim();
            return oneLine.Length <= 40 ? oneLine : oneLine.Substring(0, 40) + "…";
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
