using System;

namespace Utility.Services.Localization
{
    /// <summary>
    /// Перевод двухбуквенных кодов (<c>ru</c>, <c>en</c>) в <see cref="GameLanguage"/>
    /// и обратно. Живёт отдельно, потому что коды приходят СНАРУЖИ — от SDK площадки,
    /// из сохранения, из строки запроса, — и разбирать их в трёх местах по-разному
    /// значит получить три разных ответа на «какой сейчас язык».
    /// </summary>
    public static class LanguageCode
    {
        public static string ToCode(GameLanguage language) => language switch
        {
            GameLanguage.English => "en",
            _ => "ru",
        };

        /// <summary>
        /// Разбирает код. Принимает и <c>ru</c>, и <c>ru-RU</c>, и <c>RU</c>.
        /// Неизвестный код — НЕ ошибка: площадка вправе прислать любой из своих языков,
        /// и правильная реакция — промолчать, чтобы решение принял следующий источник
        /// (в пределе — фолбэк на русский), а не подставить наугад.
        /// </summary>
        public static bool TryParse(string code, out GameLanguage language)
        {
            language = LocalizationService.FALLBACK_LANGUAGE;
            if (string.IsNullOrWhiteSpace(code))
                return false;

            string normalized = code.Trim();
            int separator = normalized.IndexOfAny(new[] { '-', '_' });
            if (separator > 0)
                normalized = normalized.Substring(0, separator);

            if (normalized.Equals("ru", StringComparison.OrdinalIgnoreCase))
            {
                language = GameLanguage.Russian;
                return true;
            }
            if (normalized.Equals("en", StringComparison.OrdinalIgnoreCase))
            {
                language = GameLanguage.English;
                return true;
            }
            return false;
        }
    }
}
