using System;
using System.Collections.Generic;
using Core.Flask.Models;

namespace Core.Spells
{
    /// <summary>
    /// КНИГА, СОБРАННАЯ НА ЛЕТУ, — то, с чем игрок реально идёт в бой.
    ///
    /// ═══ ЗАЧЕМ ЭТО ВООБЩЕ ═══
    ///
    /// Закон меты (docs/10 §13.1): <b>пул стихий выводится из ЭКИПИРОВАННОЙ колоды,
    /// а не из купленной коллекции и не из книги целиком.</b> Из него растёт вся
    /// прогрессия: взял рецепт с новой стихией — она начала сыпаться в колбы, собрать
    /// четыре одинаковых стало труднее. «Больше заклинаний» перестаёт означать «сильнее»,
    /// и единственное, что поднимает потолок игрока, — единственное, что усложняет ему пазл.
    ///
    /// Раньше источником была книга-ассет: <c>StepsController</c> отдавал
    /// <c>Book.Combinations</c> в <see cref="Table"/> и <c>Book.UniqElements</c>
    /// в <c>FlaskController</c>. Оба уже принимают массивы, поэтому весь переход
    /// на колоду — это подменить источник, а этот класс и есть новый источник.
    ///
    /// ═══ ПОЧЕМУ ВЫВОД ПУЛА ЖИВЁТ ЗДЕСЬ, А НЕ В <c>Book.OnValidate</c> ═══
    ///
    /// Вывод нужен в двух мирах: в редакторе (книга пересчитывает своё поле
    /// <c>_uniqElements</c> при правке) и в рантайме (колода собирается из подмножества
    /// рецептов). Две копии одного вывода разъехались бы молча — и разъезд был бы виден
    /// не как ошибка, а как «в колбах не те шарики». Поэтому вывод один
    /// (<see cref="CollectUniqElements"/>), а <c>Book.OnValidate</c> его зовёт.
    /// </summary>
    public sealed class SpellDeck
    {
        /// <summary>
        /// Книга, которой принадлежит колода. Нужна не пазлу, а бою и окнам: HP героя,
        /// иконка, имя и класс живут в книге и от состава колоды не зависят.
        /// </summary>
        public Book Book { get; }

        /// <summary>Рецепты, ИЗ КОТОРЫХ СТРОИТСЯ <see cref="Table"/> этой партии.</summary>
        public Combination[] Combinations { get; }

        /// <summary>Пул стихий для <c>ElementsGenerator</c>. Он же E из docs/10 §13.6.</summary>
        public Element[] UniqElements { get; }

        public int RecipeCount => Combinations.Length;

        public int ElementCount => UniqElements.Length;

        public SpellDeck(Book book, IReadOnlyList<Combination> combinations)
        {
            Book = book;
            Combinations = ToArray(combinations);
            UniqElements = CollectUniqElements(Combinations);
        }

        /// <summary>
        /// Колода «вся книга» — поведение проекта ДО меты и фолбэк на случай, когда
        /// экипировать нечего. Пустой пул стихий — это не «лёгкая игра», а падение
        /// в <c>ElementsGenerator</c> (<c>Random.Range(0, 0)</c> и обращение к пустому
        /// списку), поэтому пустая колода в бой не выпускается никогда.
        /// </summary>
        public static SpellDeck WholeBook(Book book)
            => new SpellDeck(book, book == null ? Array.Empty<Combination>() : book.Combinations);

        /// <summary>
        /// ЕДИНСТВЕННЫЙ ВЫВОД ПУЛА СТИХИЙ в проекте. Порядок сохраняется — тот же, что
        /// у появления стихии в списке рецептов: он виден игроку (цвет колб) и не должен
        /// прыгать от пересборки.
        ///
        /// Сравнение по ССЫЛКЕ НА АССЕТ, а не по <c>Element.ID</c>, — так же, как было
        /// в <c>Book.OnValidate</c>. Два ассета с одним ID для дерева неразличимы, но
        /// в колбах это два разных шарика; ругаться на такое — работа <c>BookTreeReport</c>,
        /// а не этого метода.
        /// </summary>
        public static Element[] CollectUniqElements(IReadOnlyList<Combination> combinations)
        {
            if (combinations == null || combinations.Count == 0)
                return Array.Empty<Element>();

            List<Element> uniqElements = new List<Element>();
            foreach (Combination combination in combinations)
            {
                if (combination == null || combination.Elements == null)
                    continue;

                foreach (Element element in combination.Elements)
                {
                    if (element != null && !uniqElements.Contains(element))
                        uniqElements.Add(element);
                }
            }
            return uniqElements.ToArray();
        }

        private static Combination[] ToArray(IReadOnlyList<Combination> combinations)
        {
            if (combinations == null || combinations.Count == 0)
                return Array.Empty<Combination>();

            List<Combination> clean = new List<Combination>(combinations.Count);
            foreach (Combination combination in combinations)
                if (combination != null)
                    clean.Add(combination);

            return clean.ToArray();
        }
    }
}
