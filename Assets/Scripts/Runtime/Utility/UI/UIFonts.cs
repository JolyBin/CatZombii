using TMPro;
using UnityEngine;

namespace Utility.UI
{
    /// <summary>
    /// ШРИФТ ДЛЯ ТЕКСТА, СОЗДАННОГО КОДОМ.
    ///
    /// Существует ради одной ловушки, на которую проект уже наступал: дефолтный
    /// <c>LiberationSans SDF</c> собран БЕЗ КИРИЛЛИЦЫ, поэтому любой
    /// <see cref="TextMeshProUGUI"/>, созданный в рантайме и оставленный со шрифтом
    /// по умолчанию, показывает русскую строку рядом квадратов (docs/08 §3).
    ///
    /// Решение — брать шрифт у соседнего текста того же окна: он заведомо умеет
    /// кириллицу, иначе окно было бы нечитаемым и без нас. Приём одинаковый у всех,
    /// кто строит UI кодом (полоса сообщения, оверлей книги), поэтому живёт он
    /// в одном месте, а не копией в каждом.
    /// </summary>
    public static class UIFonts
    {
        /// <summary>
        /// Шрифт любого текста внутри <paramref name="host"/>, включая выключенные
        /// объекты. <c>null</c> — текстов нет вовсе; тогда вызывающий обязан оставить
        /// шрифт TMP по умолчанию и смириться с латиницей.
        /// </summary>
        /// <remarks>
        /// Звать ДО создания собственного текста — иначе метод найдёт его же и вернёт
        /// шрифт по умолчанию, который и надо было заменить.
        /// </remarks>
        public static TMP_FontAsset BorrowFrom(Component host)
        {
            if (host == null)
                return null;

            TextMeshProUGUI neighbour = host.GetComponentInChildren<TextMeshProUGUI>(true);
            return neighbour == null ? null : neighbour.font;
        }
    }
}
