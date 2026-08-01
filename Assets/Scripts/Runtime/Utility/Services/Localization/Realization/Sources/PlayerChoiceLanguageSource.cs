using UnityEngine;

namespace Utility.Services.Localization
{
    /// <summary>
    /// ВЫБОР ИГРОКА — источник с высшим приоритетом. Если человек однажды переключил
    /// язык руками, ни платформа, ни система не имеют права его переубеждать.
    ///
    /// Хранится в <see cref="PlayerPrefs"/> временно и осознанно: своих сейвов у проекта
    /// ещё нет (пункт 5 скоупа фазы 1, docs/09). Когда появится <c>PlayerProfile</c>,
    /// переезжает сюда одна строка, а не вся локализация.
    /// </summary>
    public class PlayerChoiceLanguageSource : ILanguageSource
    {
        private const string PREFS_KEY = "language_yg_free";

        public string Name => "выбор игрока";

        public bool TryGetLanguage(out GameLanguage language)
            => LanguageCode.TryParse(PlayerPrefs.GetString(PREFS_KEY, string.Empty), out language);

        public static void Save(GameLanguage language)
        {
            PlayerPrefs.SetString(PREFS_KEY, LanguageCode.ToCode(language));
            PlayerPrefs.Save();
        }
    }
}
