using UnityEngine;

namespace Utility.Services.Localization
{
    /// <summary>
    /// Язык операционной системы / браузера. ЧЕСТНЫЙ ФОЛБЭК на случай, когда SDK
    /// площадки не отвечает: в редакторе, в билде под другую платформу, при локальном
    /// открытии WebGL-сборки файлом.
    ///
    /// Молчит на всех языках, которых в игре нет, — и это правильный ответ, а не сбой:
    /// решение тогда принимает фолбэк на русский, а не случайное совпадение.
    /// </summary>
    public class SystemLanguageSource : ILanguageSource
    {
        public string Name => "язык системы";

        public bool TryGetLanguage(out GameLanguage language)
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Russian:
                case SystemLanguage.Belarusian:
                case SystemLanguage.Ukrainian:
                    language = GameLanguage.Russian;
                    return true;
                case SystemLanguage.English:
                    language = GameLanguage.English;
                    return true;
                default:
                    language = LocalizationService.FALLBACK_LANGUAGE;
                    return false;
            }
        }
    }
}
