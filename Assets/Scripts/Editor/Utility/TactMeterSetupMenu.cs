using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Spells;
using UnityEditor;
using UnityEngine;
using Utility.Services.Saves;

namespace CatZombii.EditorTools
{
    /// <summary>
    /// ПОДГОТОВКА ЗАМЕРА «ПЕРЕЛИВОВ НА СХЛОПЫВАНИЕ» (docs/10 §8 шаг 0, вторая метрика; §13.6).
    ///
    /// Зачем. Замерить надо ОДНО число, но трижды — при E = 2, 3 и 4 стихиях в колбах
    /// (E — длина <c>Book.UniqElements</c>, то есть пул генератора). Партия играется той
    /// книгой, которую вернул <c>HeroController.ResolveSavedBook</c>, а он берёт её
    /// из сейва по <c>PlayerProfile.HeroId</c>. Значит сменить книгу замера можно, вообще
    /// НЕ ТРОГАЯ СЦЕНУ: достаточно положить нужный <c>HeroId</c> в сейв до входа в Play Mode.
    ///
    /// Почему не через окно героев на сцене. Список героев там — сериализованный массив
    /// <c>UIHeroesWindow._uiHeroes</c>, то есть добавление временной книги означало бы
    /// правку сцены ради двух прогонов замера. Сейв даёт тот же результат ценой одной
    /// строки и откатывается одним пунктом меню.
    ///
    /// Порядок такой же, как у <see cref="SavesDebugMenu"/>, и по той же причине:
    /// сейв читается ОДИН РАЗ, на старте (<c>GameManager.Boot</c>). Меняем ДО Play Mode.
    ///
    /// Временные книги — <c>Assets/Resources/Spells/Books/TactMeter Books/</c>. Это книга
    /// Барсика плюс один-два односоставных рецепта на новых стихиях, чтобы пул генератора
    /// вырос, а игра осталась играбельной (у каждой стихии есть кэш-аут длины 1).
    /// После замера папку и это меню можно удалить целиком — на них никто не ссылается.
    /// </summary>
    public static class TactMeterSetupMenu
    {
        private const string MENU_ROOT = "Tools/Замер такта/";

        /// <summary>Идентификаторы временных книг — они же <c>Book._heroId</c> в ассетах.</summary>
        private const string HERO_ID_E3 = "tactmeter-e3";
        private const string HERO_ID_E4 = "tactmeter-e4";

        /// <summary>Имя ассета книги Барсика: у неё <c>_heroId</c> пуст, значит HeroId = имя ассета.</summary>
        private const string WARRIOR_ASSET_NAME = "Warrior Book";

        [MenuItem(MENU_ROOT + "Играть книгой E=2 (Warrior, боевая)", priority = 10)]
        public static void UseE2() => SelectBook(WARRIOR_ASSET_NAME);

        [MenuItem(MENU_ROOT + "Играть книгой E=3 (временная)", priority = 11)]
        public static void UseE3() => SelectBook(HERO_ID_E3);

        [MenuItem(MENU_ROOT + "Играть книгой E=4 (временная)", priority = 12)]
        public static void UseE4() => SelectBook(HERO_ID_E4);

        [MenuItem(MENU_ROOT + "Показать: какой книгой играем сейчас", priority = 30)]
        public static void ShowCurrent()
        {
            string heroId = ReadProfile().HeroId;
            if (string.IsNullOrEmpty(heroId))
            {
                Debug.Log("[Замер] В сейве героя нет — партия пойдёт книгой по умолчанию " +
                          "(GameManager._startBook на сцене). Число стихий смотри в шапке [TactMeter] после старта боя.");
                return;
            }

            Book book = FindBook(heroId);
            if (book == null)
            {
                Debug.LogWarning($"[Замер] В сейве герой «{heroId}», но такой книги в Resources/{BookCatalog.RESOURCES_PATH} нет. " +
                                 "Игра откатится на книгу по умолчанию — замер пойдёт не с тем E.");
                return;
            }

            Debug.Log($"[Замер] Сейчас играем книгой «{book.name}» (HeroId «{heroId}»): {Describe(book)}.", book);
        }

        [MenuItem(MENU_ROOT + "Список книг: сколько в каждой стихий", priority = 31)]
        public static void ListBooks()
        {
            List<Book> books = LoadAllBooks();
            if (books.Count == 0)
            {
                Debug.LogWarning("[Замер] Книг в проекте не найдено.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("[Замер] Книги проекта и число стихий E (длина UniqElements — пул генератора колб):");
            foreach (Book book in books.OrderBy(b => b.UniqElements == null ? 0 : b.UniqElements.Length).ThenBy(b => b.name))
                sb.AppendLine($"  E = {(book.UniqElements == null ? 0 : book.UniqElements.Length),-2} | {book.name,-22} | HeroId «{book.HeroId}» | {Describe(book)}");

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Прогоны при разных E сравнимы только на одних и тех же уровнях, поэтому уровень
        /// сбрасывается отдельным осознанным действием, а не молча вместе со сменой книги:
        /// это ПРОГРЕСС игрока, а не настройка замера.
        /// </summary>
        [MenuItem(MENU_ROOT + "Вернуть уровень 1 (для сравнимости прогонов)", priority = 32)]
        public static void ResetLevel()
        {
            PlayerProfile profile = ReadProfile();
            // С метой (docs/10 §13.4) прогресс переехал из LevelIndex в ClearedNodes:
            // «текущего уровня» больше нет, узлы перепроходимы. LevelIndex остался
            // в профиле только как легаси для миграции v1 и на прогресс не влияет.
            profile.ClearedNodes = 0;
            WriteProfile(profile);
            Debug.Log("[Замер] Уровень сброшен на 1. Прогресс уровней потерян — это цена сравнимости трёх прогонов.");
        }

        private static void SelectBook(string heroIdOrAssetName)
        {
            Book book = FindBook(heroIdOrAssetName);
            if (book == null)
            {
                Debug.LogError($"[Замер] Книга «{heroIdOrAssetName}» не найдена в Resources/{BookCatalog.RESOURCES_PATH}. " +
                               "Временные книги должны лежать в «TactMeter Books» — проверь, что папка на месте.");
                return;
            }

            PlayerProfile profile = ReadProfile();
            profile.HeroId = book.HeroId;
            WriteProfile(profile);

            string warning = EditorApplication.isPlaying
                ? " ⚠ Ты в Play Mode: сейв читается только на старте, выйди и зайди снова."
                : string.Empty;

            Debug.Log($"[Замер] Книга замера: «{book.name}» — {Describe(book)}. " +
                      $"Жми Play и играй; в консоли ищи строки [TactMeter].{warning}", book);
        }

        private static string Describe(Book book)
        {
            int count = book.UniqElements == null ? 0 : book.UniqElements.Length;
            string names = book.UniqElements == null || book.UniqElements.Length == 0
                ? "стихий нет"
                : string.Join(", ", book.UniqElements.Select(e => e == null ? "null" : e.name));
            return $"E = {count} ({names}), рецептов {(book.Combinations == null ? 0 : book.Combinations.Length)}";
        }

        private static Book FindBook(string heroIdOrAssetName)
        {
            List<Book> books = LoadAllBooks();
            foreach (Book book in books)
                if (book.HeroId == heroIdOrAssetName)
                    return book;
            foreach (Book book in books)
                if (book.name == heroIdOrAssetName)
                    return book;
            return null;
        }

        /// <summary>
        /// Ищем через <c>t:ScriptableObject</c> + фильтр в коде — тем же приёмом, что
        /// и <see cref="LocalizationAudit"/>, чтобы не зависеть от состояния индекса поиска.
        /// </summary>
        private static List<Book> LoadAllBooks()
            => AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Resources" })
                            .Select(AssetDatabase.GUIDToAssetPath)
                            .Distinct()
                            .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>)
                            .OfType<Book>()
                            .ToList();

        private static PlayerProfile ReadProfile()
        {
            string json = PlayerPrefs.GetString(PlayerPrefsSaveStorage.PREFS_KEY, string.Empty);
            return ProfileSerializer.TryFromJson(json, out PlayerProfile profile)
                ? profile
                : PlayerProfile.CreateNew();
        }

        private static void WriteProfile(PlayerProfile profile)
        {
            PlayerPrefs.SetString(PlayerPrefsSaveStorage.PREFS_KEY, ProfileSerializer.ToJson(profile));
            PlayerPrefs.Save();
        }
    }
}
