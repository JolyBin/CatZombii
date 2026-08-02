using UnityEditor;
using UnityEngine;
using Utility.Services.Saves;

namespace CatZombii.EditorTools
{
    /// <summary>
    /// РУЧНАЯ ПРОВЕРКА СОХРАНЕНИЙ. Меню + <c>Debug.Log</c>, без окна и без настроек —
    /// ровно как <see cref="LocalizationAudit"/>, и по той же причине.
    ///
    /// Зачем оно нужно. Сейвы ломаются не в редакторе, а у игрока: битый JSON, сейв
    /// из будущей версии, чужие данные под нашим ключом. Все три случая обязаны
    /// заканчиваться откатом к новому профилю и заметной строкой в логе, а НЕ падением.
    /// Проверить это можно только одним способом — подложить такой сейв руками, и без
    /// этого меню на каждую проверку уходит отдельный скрипт.
    ///
    /// Как проверять: подложить → выйти из Play Mode и зайти снова → смотреть консоль.
    /// Загрузка случается один раз, на старте (<c>GameManager.Boot</c>), поэтому
    /// подкладывать сейв «на живую» бессмысленно.
    /// </summary>
    public static class SavesDebugMenu
    {
        private const string MENU_ROOT = "Tools/Сохранения/";

        [MenuItem(MENU_ROOT + "Показать сейв")]
        public static void Show()
        {
            string json = PlayerPrefs.GetString(PlayerPrefsSaveStorage.PREFS_KEY, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                Debug.Log($"[Saves] Сейва нет (ключ «{PlayerPrefsSaveStorage.PREFS_KEY}» пуст).");
                return;
            }

            Debug.Log($"[Saves] Сырой сейв: {json}");
            Debug.Log(ProfileSerializer.TryFromJson(json, out PlayerProfile profile)
                ? $"[Saves] Читается: {profile}"
                : "[Saves] НЕ ЧИТАЕТСЯ — подробности выше.");
        }

        [MenuItem(MENU_ROOT + "Стереть сейв")]
        public static void Delete()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsSaveStorage.PREFS_KEY);
            PlayerPrefs.Save();
            Debug.Log("[Saves] Сейв стёрт. Следующий запуск будет как первый.");
        }

        [MenuItem(MENU_ROOT + "Испортить сейв (обрезать JSON)")]
        public static void Corrupt()
        {
            // Именно обрыв строки, а не случайный мусор: так сейв и бьётся в жизни —
            // вкладку закрыли посреди записи в IndexedDB.
            PlayerPrefs.SetString(PlayerPrefsSaveStorage.PREFS_KEY, "{\"Version\":1,\"LevelIndex\":3,\"HeroI");
            PlayerPrefs.Save();
            Debug.Log("[Saves] Сейв испорчен. Перезайди в Play Mode: ожидается ошибка в логе и уровень 1.");
        }

        [MenuItem(MENU_ROOT + "Подложить сейв из будущей версии")]
        public static void FromFuture()
        {
            // Живой сценарий: на площадке откатили релиз, а у игрока сейв от новой сборки.
            PlayerPrefs.SetString(PlayerPrefsSaveStorage.PREFS_KEY,
                $"{{\"Version\":{PlayerProfile.CURRENT_VERSION + 1},\"LevelIndex\":4,\"HeroId\":\"\",\"Coins\":0,\"LanguageCode\":\"\"}}");
            PlayerPrefs.Save();
            Debug.Log("[Saves] Подложен сейв новее игры. Перезайди в Play Mode: ожидается ошибка в логе и уровень 1.");
        }

        /// <summary>
        /// СЕЙВ ИЗ ПРОШЛОЙ ВЕРСИИ, СЛОВО В СЛОВО. Ровно те поля, что писала v1
        /// (Version / LevelIndex / HeroId / Coins / LanguageCode) — ни одного из полей
        /// меты в нём нет и быть не может.
        /// </summary>
        private const string SAVE_V1 =
            "{\"Version\":1,\"LevelIndex\":3,\"HeroId\":\"Warrior Book\",\"Coins\":250,\"LanguageCode\":\"ru\"}";

        [MenuItem(MENU_ROOT + "Подложить сейв v1 (до меты)")]
        public static void FromV1()
        {
            PlayerPrefs.SetString(PlayerPrefsSaveStorage.PREFS_KEY, SAVE_V1);
            PlayerPrefs.Save();
            Debug.Log("[Saves] Подложен сейв v1. Перезайди в Play Mode: ожидается «Сейв смигрирован: v1 → v2», " +
                      "пройдено 3 узла, герой Warrior Book, 250 клубков, 4 слота колоды.");
        }

        /// <summary>
        /// ПРОВЕРКА МИГРАЦИИ БЕЗ PLAY MODE. Разбирает сейв v1 прямо здесь и сверяет
        /// результат с тем, что обещает <c>ProfileMigration</c>.
        ///
        /// Почему это отдельный пункт меню, а не «подложить и посмотреть глазами»:
        /// миграция — единственный код проекта, ошибка в котором стоит прогресса живых
        /// игроков и при этом НЕ ВИДНА (сейв прочитался, игра запустилась, просто
        /// половина полей нули). Такое ловится только сверкой с ожиданием.
        /// </summary>
        [MenuItem(MENU_ROOT + "Проверить миграцию v1 → v2")]
        public static void CheckMigration()
        {
            int errors = 0;

            if (!ProfileSerializer.TryFromJson(SAVE_V1, out PlayerProfile profile))
            {
                Debug.LogError("[Saves] ПРОВАЛ: сейв v1 не прочитался вовсе. Подробности выше.");
                return;
            }

            errors += Expect(profile.Version == PlayerProfile.CURRENT_VERSION,
                             $"версия поднята до {PlayerProfile.CURRENT_VERSION}", $"версия {profile.Version}");
            errors += Expect(profile.ClearedNodes == 3, "пройдено узлов 3", $"пройдено {profile.ClearedNodes}");
            errors += Expect(profile.LevelIndex == 0, "легаси-поле LevelIndex обнулено", $"LevelIndex {profile.LevelIndex}");
            errors += Expect(profile.Coins == 250, "кошелёк цел (250)", $"клубков {profile.Coins}");
            errors += Expect(profile.HeroId == "Warrior Book", "герой цел", $"герой «{profile.HeroId}»");
            errors += Expect(profile.LanguageCode == "ru", "язык цел", $"язык «{profile.LanguageCode}»");
            errors += Expect(profile.DeckSlots == PlayerProfile.BASE_DECK_SLOTS,
                             $"слотов колоды {PlayerProfile.BASE_DECK_SLOTS}", $"слотов {profile.DeckSlots}");
            errors += Expect(profile.IsHeroUnlocked("Warrior Book"), "сыгранный герой остался открытым", "герой закрыт");
            errors += Expect(profile.Heroes != null && profile.Heroes.Length == 0,
                             "записей героев нет — стартовую колоду выдаст первый вход",
                             $"записей {(profile.Heroes == null ? -1 : profile.Heroes.Length)}");

            // Обратная дорога: то, что смигрировали, обязано ещё и записаться.
            string json = ProfileSerializer.ToJson(profile);
            errors += Expect(ProfileSerializer.TryFromJson(json, out PlayerProfile round) && round.ClearedNodes == 3,
                             "перезапись и повторное чтение не теряют прогресс", "круг чтение-запись потерял данные");

            // И отдельно — что чужой json по-прежнему НЕ считается профилем.
            errors += Expect(!ProfileSerializer.TryFromJson("{\"foo\":1}", out _),
                             "чужой json отвергнут (инициализаторов у полей нет)", "чужой json прочитался как профиль");

            Debug.Log(errors == 0
                ? $"[Saves] Миграция v1 → v2 в порядке. Прочитано: {profile}"
                : $"[Saves] МИГРАЦИЯ СЛОМАНА: расхождений {errors}. Смотри сообщения выше.");
        }

        private static int Expect(bool ok, string expected, string actual)
        {
            if (ok)
                return 0;
            Debug.LogError($"[Saves] ПРОВАЛ: ожидалось — {expected}; получено — {actual}.");
            return 1;
        }
    }
}
