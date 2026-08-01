using System;

namespace Utility.Services.Localization
{
    /// <summary>
    /// Единственный источник пользовательского текста в игре.
    ///
    /// Правило проекта: НИ ОДНОЙ пользовательской строки литералом — ни в коде,
    /// ни в ассете, ни в TMP на сцене. Только ключ, который разрешает этот сервис.
    /// Рецепт — docs/05 «Добавить строку».
    /// </summary>
    public interface ILocalizationService
    {
        /// <summary>Текущий язык. Меняется через <see cref="SetLanguage"/>.</summary>
        GameLanguage Language { get; }

        /// <summary>
        /// Язык сменился. Подписчик обязан перечитать свои строки.
        /// Живой сценарий: игрок сменил язык в интерфейсе Яндекс.Игр,
        /// не перезагружая вкладку.
        /// </summary>
        event Action OnLanguageChanged;

        /// <summary>
        /// Строка по ключу. Отсутствующий ключ НЕ превращается в пустоту:
        /// возвращается заметный маркер вида <c>#ключ#</c> и пишется ошибка в лог.
        /// Молчаливая пустота в UI — дефект, который находят игроки, а не разработчик.
        /// </summary>
        string Get(string key);

        /// <summary>
        /// Строка по ключу с подстановкой: шаблон <c>«Волна: {0}/{1}»</c>.
        /// Кривой шаблон не бросает исключение в UI — он логируется, а игроку
        /// уходит сам шаблон.
        /// </summary>
        string Get(string key, params object[] args);

        /// <summary>Есть ли ключ в таблице вообще — для редакторных проверок.</summary>
        bool HasKey(string key);

        void SetLanguage(GameLanguage language);
    }
}
