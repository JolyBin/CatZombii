using System;
using System.Collections.Generic;
using System.Text;
using Core.Flask.Models;

namespace Core.Spells
{
    /// <summary>
    /// ЧЕМ РЕЦЕПТ ЗАПИСАН В СЕЙВЕ. Мета обязана хранить «этот рецепт куплен» и «этот
    /// рецепт в колоде», а в JSON не положить ни ссылку на <see cref="Combination"/>,
    /// ни ссылку на <see cref="Element"/>.
    ///
    /// ═══ ПОЧЕМУ ИДЕНТИФИКАТОР — САМА ЦЕПОЧКА, А НЕ ИМЯ АССЕТА ═══
    ///
    /// Прецедент в проекте — <c>Book.HeroId</c> (имя ассета с возможностью переопределить).
    /// Для рецепта он не годится, и вот почему:
    ///
    /// 1. РЕЦЕПТ И ЕСТЬ ЦЕПОЧКА. <see cref="Table"/> строит trie по <c>Element.ID</c>
    ///    и по построению не различает два ассета с одинаковой цепочкой — второй молча
    ///    затирает первый (это же ловит <c>BookTreeReport</c> как поломку данных).
    ///    Значит цепочка уже уникальна внутри книги, и заводить рядом ВТОРОЙ ключ
    ///    означало бы завести второй источник истины и рассинхрон между ними.
    /// 2. ПРАВИЛО ПРЕФИКСОВ (docs/10 §13.2) формулируется В ТЕРМИНАХ ЦЕПОЧКИ.
    ///    С именем ассета «какие префиксы нужны для этого рецепта» требует обратного
    ///    поиска по книге на каждый вопрос; с цепочкой это <c>Substring</c>.
    /// 3. ID СТИХИЙ ЗАФИКСИРОВАНЫ ДИЗАЙНОМ. docs/10 §13.5: «семь стихий уже есть,
    ///    менять существующие ID нельзя». То есть основа ключа объявлена неизменной
    ///    тем же документом, который вводит мету.
    ///
    /// ЦЕНА, которую надо знать: если геймдизайнер ПОМЕНЯЕТ ЦЕПОЧКУ существующего
    /// рецепта (был <c>Огонь&gt;Огонь</c>, стал <c>Огонь&gt;Сила</c>), для игрока это
    /// другой рецепт — покупка старого потеряется, а из колоды он выпадет
    /// (<c>HeroLoadout</c> молча выбрасывает неизвестные книге идентификаторы).
    /// Переименование ассета, наоборот, не стоит ничего — в отличие от <c>Book.HeroId</c>.
    /// Обмен сознательный: цепочки правят до релиза, ассеты переименовывают всегда.
    ///
    /// Формат — <c>«3&gt;3&gt;4»</c>: человекочитаемо в баг-репорте (это требование
    /// к формату сейва, см. <c>ProfileSerializer</c>) и диффится глазом.
    /// </summary>
    public static class RecipeId
    {
        /// <summary>Разделитель звеньев. Тот же символ, которым дерево печатается в отчётах.</summary>
        public const char SEPARATOR = '>';

        /// <summary>
        /// Идентификатор рецепта. Пустая строка — рецепта НЕ СУЩЕСТВУЕТ (пустой список
        /// стихий или <c>null</c> внутри него): ровно те два случая, которые
        /// <c>BookTreeReport</c> называет поломкой данных, и здесь они не должны
        /// превращаться в правдоподобный ключ.
        /// </summary>
        public static string Of(Combination combination)
            => combination == null ? string.Empty : Of(combination.Elements);

        public static string Of(IReadOnlyList<Element> elements)
        {
            if (elements == null || elements.Count == 0)
                return string.Empty;

            StringBuilder builder = new StringBuilder(elements.Count * 2);
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i] == null)
                    return string.Empty;
                if (i > 0)
                    builder.Append(SEPARATOR);
                builder.Append(elements[i].ID);
            }
            return builder.ToString();
        }

        /// <summary>Длина рецепта в звеньях. Она же — сколько тактов он стоит в котле.</summary>
        public static int Length(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId))
                return 0;

            int count = 1;
            for (int i = 0; i < recipeId.Length; i++)
                if (recipeId[i] == SEPARATOR)
                    count++;
            return count;
        }

        /// <summary>
        /// Родительский узел дерева: <c>«3&gt;3&gt;4»</c> → <c>«3&gt;3»</c>.
        /// Пустая строка — родителя нет (рецепт длины 1 или пустой ключ).
        /// </summary>
        public static string ParentOf(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId))
                return string.Empty;

            int last = recipeId.LastIndexOf(SEPARATOR);
            return last < 0 ? string.Empty : recipeId.Substring(0, last);
        }

        /// <summary>
        /// ВСЕ СОБСТВЕННЫЕ ПРЕФИКСЫ, от короткого к длинному: для <c>«3&gt;3&gt;4»</c>
        /// это <c>«3»</c>, <c>«3&gt;3»</c>. Сам рецепт в список НЕ входит.
        ///
        /// Порядок значащий: правило колоды (docs/10 §13.2) требует брать префиксы
        /// снизу вверх, и в этом же порядке их надо показать игроку.
        /// </summary>
        public static List<string> ProperPrefixes(string recipeId)
        {
            List<string> prefixes = new List<string>();
            if (string.IsNullOrEmpty(recipeId))
                return prefixes;

            for (int i = 0; i < recipeId.Length; i++)
                if (recipeId[i] == SEPARATOR)
                    prefixes.Add(recipeId.Substring(0, i));

            return prefixes;
        }

        /// <summary>
        /// Префикс ли. Сравнение ПОЗВЕННОЕ, а не по символам: <c>«3»</c> обязано быть
        /// префиксом <c>«3&gt;4»</c> и не быть префиксом <c>«31&gt;4»</c>. Стихий сегодня
        /// семь и двузначных ID нет, но ловушка стоит одной проверки, а её отсутствие
        /// проявилось бы как «рецепт не берётся, а почему — непонятно».
        /// </summary>
        public static bool IsPrefixOf(string prefix, string recipeId)
        {
            if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(recipeId))
                return false;
            if (prefix.Length >= recipeId.Length)
                return false;

            return string.CompareOrdinal(recipeId, 0, prefix, 0, prefix.Length) == 0
                   && recipeId[prefix.Length] == SEPARATOR;
        }

        /// <summary>
        /// Читаемая запись цепочки для игрока и для лога: <c>«Огонь &gt; Огонь &gt; Сила»</c>.
        /// Имена стихий берутся из книги, потому что <see cref="Element"/> — ассет,
        /// а идентификатор знает только числа.
        /// </summary>
        public static string Describe(string recipeId, IReadOnlyList<Element> vocabulary)
        {
            if (string.IsNullOrEmpty(recipeId))
                return string.Empty;
            if (vocabulary == null || vocabulary.Count == 0)
                return recipeId;

            string[] parts = recipeId.Split(SEPARATOR);
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                    builder.Append(' ').Append(SEPARATOR).Append(' ');
                builder.Append(NameOf(parts[i], vocabulary));
            }
            return builder.ToString();
        }

        private static string NameOf(string idText, IReadOnlyList<Element> vocabulary)
        {
            if (!int.TryParse(idText, out int id))
                return idText;

            foreach (Element element in vocabulary)
                if (element != null && element.ID == id)
                    return element.name;

            return idText;
        }

        /// <summary>Числовые ID звеньев. Нужен всем, кто ходит по дереву стихиями, а не строками.</summary>
        public static int[] ToElementIds(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId))
                return Array.Empty<int>();

            string[] parts = recipeId.Split(SEPARATOR);
            int[] ids = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                ids[i] = int.TryParse(parts[i], out int id) ? id : 0;
            return ids;
        }
    }
}
