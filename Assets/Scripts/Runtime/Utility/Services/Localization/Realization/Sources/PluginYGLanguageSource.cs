using UnityEngine;

namespace Utility.Services.Localization
{
    /// <summary>
    /// ЕДИНСТВЕННОЕ МЕСТО ВО ВСЁМ ПРОЕКТЕ, где локализация знает про PluginYG.
    ///
    /// Требование docs/09: SDK-слой держим изолированным, иначе выход на мобилки
    /// и Steam дорожает. Поэтому игровой код обращается только к
    /// <see cref="ILocalizationService"/>, а площадка — всего лишь один из
    /// <see cref="ILanguageSource"/>, и удаление этого файла целиком ничего не ломает.
    ///
    /// ПРО ДВОЙНОЙ <c>#if</c> — это не перестраховка, а факт проекта на 02.08.2026:
    /// дефайн <c>PLUGIN_YG_2</c> стоит (в том числе на Standalone, то есть и в редакторе),
    /// но МОДУЛЬ ЛОКАЛИЗАЦИИ PluginYG в проект НЕ УСТАНОВЛЕН — папки
    /// <c>Assets/PluginYourGames/Modules/</c> нет вовсе, а единственное упоминание
    /// его API (<c>YG2.GetLanguage/SwitchLanguage</c> в <c>EventsYG2.cs</c>) само
    /// закрыто дефайном <c>Localization_yg</c>, который модуль и выставляет.
    /// Guard только по <c>PLUGIN_YG_2</c> уронил бы компиляцию сегодня же.
    ///
    /// Как включить, когда модуль поставят: PluginYG → установить модуль Localization
    /// (он сам добавит дефайн <c>Localization_yg</c>) и зарегистрировать этот источник
    /// в <see cref="Localization"/> — больше ничего.
    /// </summary>
    public class PluginYGLanguageSource : ILanguageSource
    {
        public string Name => "PluginYG (площадка)";

        public bool TryGetLanguage(out GameLanguage language)
        {
#if PLUGIN_YG_2 && Localization_yg
            return LanguageCode.TryParse(YG.YG2.lang, out language);
#else
            // Плагина или его модуля локализации нет — источник честно молчит,
            // и язык определит следующий (система), а в пределе — фолбэк на русский.
            language = LocalizationService.FALLBACK_LANGUAGE;
            return false;
#endif
        }
    }
}
