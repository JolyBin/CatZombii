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
    }
}
