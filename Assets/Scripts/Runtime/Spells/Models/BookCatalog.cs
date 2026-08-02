using UnityEngine;

namespace Core.Spells
{
    /// <summary>
    /// ПОИСК КНИГИ ПО ИДЕНТИФИКАТОРУ ИЗ СЕЙВА. Мост между «строкой в JSON» и ассетом:
    /// профиль хранит <c>HeroId</c>, а игре нужен <see cref="Book"/>.
    ///
    /// Почему <c>Resources.LoadAll</c>, а не список ссылок в <c>GameManager</c>.
    /// Список пришлось бы держать в двух местах — в инспекторе окна героев и в инспекторе
    /// точки входа, — и рассинхрон между ними выглядел бы как «сейв не читается».
    /// Папка книг и так одна, <c>Resources.Load*</c> работает на WebGL без Addressables
    /// (тот же довод, по которому в <c>Resources/</c> лежат все конфиги проекта), а сам
    /// поиск случается ровно один раз за запуск — на старте, когда читается профиль.
    /// </summary>
    public static class BookCatalog
    {
        /// <summary>Путь внутри <c>Resources/</c>. Меняешь папку — меняй здесь, второго места нет.</summary>
        public const string RESOURCES_PATH = "Spells/Books";

        private static Book[] _books;

        /// <summary>Все книги проекта. Грузятся один раз за запуск.</summary>
        public static Book[] All => _books ??= Resources.LoadAll<Book>(RESOURCES_PATH);

        /// <summary>
        /// Найти книгу по идентификатору. <c>null</c> — не нашли; вызывающий обязан
        /// откатиться на героя по умолчанию, а не остаться без книги: без книги
        /// не соберётся ни одна комбинация, то есть игра будет запущена, но не играбельна.
        /// </summary>
        public static Book Find(string heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId))
                return null;

            foreach (Book book in All)
            {
                if (book != null && book.HeroId == heroId)
                    return book;
            }
            return null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Кэш ассетов переживает выход из Play Mode при выключенном Domain Reload,
        /// а вместе с ним — ссылки на выгруженные объекты.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsForEditor() => _books = null;
#endif
    }
}
