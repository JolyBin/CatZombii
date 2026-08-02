using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Flask.Models;
using Core.Spells;
using UnityEditor;
using UnityEngine;

namespace CatZombii.EditorTools
{
    /// <summary>
    /// КАРТА ДЕРЕВА КНИГИ. Это ОТЧЁТ, а не проверка: он ничего не запрещает и никого
    /// не ругает, он показывает геймдизайнеру форму того, что он собрал.
    ///
    /// ═══ ЧТО ЗДЕСЬ СОЗНАТЕЛЬНО НЕ СЧИТАЕТСЯ ОШИБКОЙ ═══
    ///
    /// Узел без заклинания — НОРМАЛЬНЫЙ промежуточный узел, и код обрабатывает его штатно:
    /// <c>Table.TryGetSpell</c> возвращает <c>spell != null</c>, то есть на таком узле варка
    /// просто не срабатывает. Ни одной строкой это не баг.
    ///
    /// Смысл отчёта в другом. Закон префикса (docs/10 §6) существует ради дизайна: на каждом
    /// шаге к глубокому рецепту у игрока должен быть выбор «забрать сейчас или достроить».
    /// Если на узле заклинания нет — выбора на этом шаге не существует, игрок обязан идти
    /// дальше. Это МОЖЕТ быть осознанным решением («глубокая ветка, назад дороги нет»),
    /// а может быть случайностью, которую никто не заметил. Различить эти два случая может
    /// только автор книги — отчёт лишь кладёт факт ему на стол.
    ///
    /// ═══ ЧТО ЗДЕСЬ ВСЁ-ТАКИ ЗОВЁТСЯ ПОЛОМКОЙ ═══
    ///
    /// Только то, чего игра физически не увидит или увидит не так, как написано в ассете:
    /// пустой список элементов у комбинации (её трижды нет в дереве), <c>null</c> внутри
    /// списка (падение в <c>Table</c> на первом же построении), две комбинации с одной
    /// и той же цепочкой (вторая молча затирает первую), два разных ассета стихии
    /// с одинаковым ID (в дереве это одна и та же ветка). Это не про дизайн, это про данные.
    ///
    /// Сделано в духе <see cref="LocalizationAudit"/>: пункт меню и <c>Debug.Log</c>,
    /// без окна, без настроек, со ссылкой на ассет.
    /// </summary>
    public static class BookTreeReport
    {
        private const string MENU_ROOT = "Tools/Книга/";

        /// <summary>Потолок котла по docs/10 §4 — рецепт длиннее набрать физически нечем.</summary>
        private const int CAULDRON_SLOTS = 4;

        [MenuItem(MENU_ROOT + "Показать дерево рецептов %#k")]
        public static void Report()
        {
            List<Book> books = LoadAllBooks();
            if (books.Count == 0)
            {
                Debug.Log("[Книга] Книг в проекте не найдено — показывать нечего.");
                return;
            }

            foreach (Book book in books.OrderBy(b => b.name))
                ReportBook(book);

            Debug.Log($"[Книга] Готово: разобрано книг — {books.Count}. " +
                      "Это карта, а не проверка: промежуточный узел без заклинания — нормальный узел дерева.");
        }

        [MenuItem(MENU_ROOT + "Показать дерево только выделенной книги", validate = true)]
        private static bool ReportSelectedValidate() => Selection.activeObject is Book;

        [MenuItem(MENU_ROOT + "Показать дерево только выделенной книги")]
        public static void ReportSelected() => ReportBook((Book)Selection.activeObject);

        // =====================================================================================
        // Дерево
        // =====================================================================================

        private sealed class Node
        {
            public int ElementId;
            public string ElementName;
            public Node Parent;
            public BaseSpellConfig Spell;
            public Combination Source;
            public readonly List<Node> Children = new List<Node>();

            /// <summary>Глубина = длина рецепта, который приводит в этот узел. Корень — 0.</summary>
            public int Depth;

            /// <summary>«Fighting &gt; Fighting &gt; Fire». У корня — пусто.</summary>
            public string Path = string.Empty;

            /// <summary>
            /// Сколько шагов ПОДРЯД игрок уже прошёл без права забрать награду, стоя в этом
            /// узле. У узла с заклинанием — 0 (кэш-аут есть прямо здесь).
            /// </summary>
            public int StepsWithoutCashout;

            public bool HasSpell => Spell != null;
        }

        private static void ReportBook(Book book)
        {
            if (book == null)
                return;

            var breakage = new List<string>();
            Node root = BuildTree(book, breakage);
            List<Node> all = Flatten(root);

            var sb = new StringBuilder();
            AppendHeader(sb, book, all);
            AppendTree(sb, root);
            AppendBranchPrice(sb, all);
            AppendDeadEnds(sb, book, root, all);
            AppendBreakage(sb, breakage);

            Debug.Log(sb.ToString(), book);

            // Карта остаётся картой и ни на что не ругается. Но расхождение АССЕТА С ИГРОЙ —
            // это не дизайн, а данные, и оно обязано быть заметно в консоли отдельно,
            // иначе утонет в тексте отчёта.
            if (breakage.Count > 0)
                Debug.LogWarning($"[Книга] «{book.name}»: мест, где игра прочитает ассет не так, как он выглядит, — " +
                                 $"{breakage.Count}. Список в отчёте выше, раздел «НЕ РАБОТАЕТ ТАК, КАК НАПИСАНО В АССЕТЕ».", book);
        }

        /// <summary>
        /// Дерево строится ТЕМ ЖЕ обходом, что и <see cref="Table.GenerateCombinationsDict"/>,
        /// включая его поведение в спорных случаях, — иначе отчёт показывал бы не ту книгу,
        /// в которую играют. В частности: ключ узла — <c>Element.ID</c>, а не сам ассет,
        /// и последняя комбинация с одинаковой цепочкой перетирает предыдущую.
        /// </summary>
        private static Node BuildTree(Book book, List<string> breakage)
        {
            var root = new Node { ElementId = 0, ElementName = "котёл пуст", Depth = 0 };
            Combination[] combinations = book.Combinations ?? System.Array.Empty<Combination>();

            // ID -> ассеты стихий, которые его занимают: два разных ассета с одним ID
            // в дереве неразличимы, и это надо показать до, а не после вёрстки
            var idOwners = new Dictionary<int, HashSet<string>>();

            for (int c = 0; c < combinations.Length; c++)
            {
                Combination combination = combinations[c];
                if (combination == null)
                {
                    breakage.Add($"в списке рецептов книги пустая ячейка №{c + 1} — игра её просто пропустит");
                    continue;
                }

                Element[] elements = combination.Elements;
                if (elements == null || elements.Length == 0)
                {
                    breakage.Add($"«{combination.name}»: список стихий пуст — рецепта НЕ СУЩЕСТВУЕТ, " +
                                 "заклинание к нему не привязано ни к одному узлу");
                    continue;
                }

                Node current = root;
                bool broken = false;

                foreach (Element element in elements)
                {
                    if (element == null)
                    {
                        breakage.Add($"«{combination.name}»: в списке стихий пустая ссылка — " +
                                     "на этом рецепте Table падает при построении книги (NullReference)");
                        broken = true;
                        break;
                    }

                    if (!idOwners.TryGetValue(element.ID, out HashSet<string> owners))
                    {
                        owners = new HashSet<string>();
                        idOwners.Add(element.ID, owners);
                    }
                    owners.Add(element.name);

                    Node child = current.Children.FirstOrDefault(n => n.ElementId == element.ID);
                    if (child == null)
                    {
                        child = new Node
                        {
                            ElementId = element.ID,
                            ElementName = element.name,
                            Parent = current,
                            Depth = current.Depth + 1,
                            Path = current.Depth == 0 ? element.name : current.Path + " > " + element.name,
                        };
                        current.Children.Add(child);
                    }
                    current = child;
                }

                if (broken)
                    continue;

                if (current.Spell != null)
                    breakage.Add($"цепочка «{current.Path}» задана дважды: «{current.Source?.name}» и «{combination.name}». " +
                                 $"В игре сработает последняя — «{combination.name}»");

                if (combination.Spell == null)
                    breakage.Add($"«{combination.name}» (цепочка «{current.Path}»): заклинание не назначено — " +
                                 "узел останется промежуточным, варка на нём не сработает");

                current.Spell = combination.Spell;
                current.Source = combination;

                if (elements.Length > CAULDRON_SLOTS)
                    breakage.Add($"«{combination.name}»: длина {elements.Length} при котле в {CAULDRON_SLOTS} слота — " +
                                 "набрать такую цепочку нечем, рецепт недостижим");
            }

            foreach (KeyValuePair<int, HashSet<string>> pair in idOwners)
                if (pair.Value.Count > 1)
                    breakage.Add($"ID стихии {pair.Key} занят сразу несколькими ассетами ({string.Join(", ", pair.Value)}) — " +
                                 "в дереве это ОДНА И ТА ЖЕ ветка, рецепты на них неразличимы");

            ComputeStepsWithoutCashout(root, 0);
            return root;
        }

        private static void ComputeStepsWithoutCashout(Node node, int inherited)
        {
            node.StepsWithoutCashout = node.Depth == 0 || node.HasSpell ? 0 : inherited + 1;
            foreach (Node child in node.Children)
                ComputeStepsWithoutCashout(child, node.StepsWithoutCashout);
        }

        private static List<Node> Flatten(Node root)
        {
            var result = new List<Node>();
            void Walk(Node node)
            {
                if (node.Depth > 0)
                    result.Add(node);
                foreach (Node child in node.Children.OrderBy(c => c.ElementName))
                    Walk(child);
            }
            Walk(root);
            return result;
        }

        // =====================================================================================
        // Печать
        // =====================================================================================

        private static void AppendHeader(StringBuilder sb, Book book, List<Node> all)
        {
            int withSpell = all.Count(n => n.HasSpell);
            int intermediate = all.Count - withSpell;
            int maxLength = all.Count == 0 ? 0 : all.Max(n => n.Depth);

            string elements = book.UniqElements == null || book.UniqElements.Length == 0
                ? "стихий нет"
                : string.Join(", ", book.UniqElements.Select(e => e == null ? "null" : e.name));

            sb.AppendLine($"[Книга] ═══ «{book.name}» ({Loc(book.NameHeroKey)}, {Loc(book.ClassHeroKey)}) ═══");
            sb.AppendLine($"Стихий в книге (E): {(book.UniqElements == null ? 0 : book.UniqElements.Length)} — {elements}");
            sb.AppendLine($"Рецептов в ассете: {(book.Combinations == null ? 0 : book.Combinations.Length)} · " +
                          $"узлов в дереве: {all.Count} · с заклинанием: {withSpell} · промежуточных: {intermediate}");
            sb.AppendLine($"Самый длинный рецепт: {maxLength} (котёл держит {CAULDRON_SLOTS})");
            sb.AppendLine();
        }

        private static void AppendTree(StringBuilder sb, Node root)
        {
            sb.AppendLine("ДЕРЕВО.  [+] — на узле есть заклинание, игрок может забрать прямо здесь.");
            sb.AppendLine("         [ ] — промежуточный узел: забрать нечего, дорога только дальше.");
            sb.AppendLine();
            AppendSubtree(sb, root);
            sb.AppendLine();
        }

        private static void AppendSubtree(StringBuilder sb, Node node)
        {
            foreach (Node child in node.Children.OrderBy(c => c.ElementName))
            {
                string indent = new string(' ', 2 + (child.Depth - 1) * 2);
                string mark = child.HasSpell ? "[+]" : "[ ]";
                string tail = child.HasSpell
                    ? $"-> «{Loc(child.Spell.NameKey)}»  ({child.Spell.name}, предпросмотр {child.Spell.PreviewValue})"
                    : "промежуточный узел — кэш-аута здесь нет";

                sb.Append($"{indent}{mark} {child.Path,-38} {tail}");

                if (child.HasSpell && StepsBefore(child) > 0)
                    sb.Append($"   [до него {Steps(StepsBefore(child))} без кэш-аута]");
                if (!child.HasSpell && child.Children.Count == 0)
                    sb.Append("   [и продолжений нет — ветка обрывается]");

                sb.AppendLine();
                AppendSubtree(sb, child);
            }
        }

        /// <summary>
        /// ЦЕНА ВЕТКИ. Ровно то, ради чего отчёт и делался: сколько ходов подряд игрок
        /// обязан идти вперёд, не имея права остановиться и забрать награду.
        /// </summary>
        private static void AppendBranchPrice(StringBuilder sb, List<Node> all)
        {
            List<Node> intermediates = all.Where(n => !n.HasSpell).ToList();
            if (intermediates.Count == 0)
            {
                sb.AppendLine("ЦЕНА ВЕТОК: кэш-аут есть на КАЖДОМ узле — остановиться можно в любой момент.");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("ЦЕНА ВЕТОК — где игрок идёт вперёд без права забрать награду:");
            foreach (Node node in intermediates.OrderByDescending(n => n.StepsWithoutCashout).ThenBy(n => n.Path))
            {
                List<string> next = NearestCashouts(node);
                string where = next.Count == 0
                    ? "дальше кэш-аута нет вовсе — из этого узла награду не забрать никогда"
                    : "ближайший кэш-аут: " + string.Join(" | ", next);
                sb.AppendLine($"  «{node.Path}» — {Steps(node.StepsWithoutCashout)} подряд без кэш-аута; {where}");
            }

            int worst = intermediates.Max(n => n.StepsWithoutCashout);
            sb.AppendLine($"  Самая дорогая цепочка: {Steps(worst)} подряд без права остановиться.");
            sb.AppendLine();
        }

        /// <summary>
        /// ЧТО ЛОМАЕТ ЦЕПОЧКУ. Игрок не выбирает, что схлопнется, — только когда нажать
        /// варку (docs/10 §6, оговорка про провальные цепочки). Поэтому для автора книги
        /// важно видеть не только то, что в дереве есть, но и то, чего в нём нет.
        /// </summary>
        private static void AppendDeadEnds(StringBuilder sb, Book book, Node root, List<Node> all)
        {
            Element[] pool = book.UniqElements;
            if (pool == null || pool.Length == 0)
                return;

            var poolIds = new List<KeyValuePair<int, string>>();
            foreach (Element element in pool)
                if (element != null && !poolIds.Any(p => p.Key == element.ID))
                    poolIds.Add(new KeyValuePair<int, string>(element.ID, element.name));

            sb.AppendLine("ЧТО ЛОМАЕТ ЦЕПОЧКУ — какая стихия из пула НЕ продолжает узел:");

            var nodes = new List<Node> { root };
            nodes.AddRange(all);

            foreach (Node node in nodes)
            {
                if (node.Depth >= CAULDRON_SLOTS)
                    continue;

                List<string> breakers = poolIds
                    .Where(p => node.Children.All(c => c.ElementId != p.Key))
                    .Select(p => p.Value)
                    .ToList();

                if (breakers.Count == 0)
                    continue;

                string from = node.Depth == 0 ? "пустой котёл" : $"«{node.Path}»";
                string continues = node.Children.Count == 0
                    ? "ничего"
                    : string.Join(", ", node.Children.OrderBy(c => c.ElementName).Select(c => c.ElementName));

                sb.AppendLine($"  {from}: продолжают — {continues}; обрывают — {string.Join(", ", breakers)}");
            }

            sb.AppendLine("  (обрыв не равен потере: варка на сломанной цепочке просто не сработает и очистит котёл)");
            sb.AppendLine();
        }

        private static void AppendBreakage(StringBuilder sb, List<string> breakage)
        {
            if (breakage.Count == 0)
            {
                sb.AppendLine("Данные книги целы: пустых ссылок, дублей цепочек и рецептов без заклинания нет.");
                return;
            }

            sb.AppendLine("НЕ РАБОТАЕТ ТАК, КАК НАПИСАНО В АССЕТЕ (это про данные, а не про дизайн):");
            foreach (string line in breakage)
                sb.AppendLine($"  · {line}");
        }

        // =====================================================================================
        // Мелочи
        // =====================================================================================

        /// <summary>Ближайшие узлы с заклинанием ниже по дереву — куда игрок обязан дойти.</summary>
        private static List<string> NearestCashouts(Node node)
        {
            var result = new List<string>();
            void Walk(Node current)
            {
                foreach (Node child in current.Children.OrderBy(c => c.ElementName))
                {
                    if (child.HasSpell)
                        result.Add($"«{child.Path}» через {Steps(child.Depth - node.Depth)}");
                    else
                        Walk(child);
                }
            }
            Walk(node);
            return result;
        }

        /// <summary>Сколько шагов без кэш-аута пришлось пройти, чтобы попасть В этот узел.</summary>
        private static int StepsBefore(Node node)
            => node.Parent == null ? 0 : node.Parent.StepsWithoutCashout;

        private static string Steps(int count)
        {
            int tail = count % 100;
            if (tail >= 11 && tail <= 14)
                return $"{count} шагов";
            switch (count % 10)
            {
                case 1: return $"{count} шаг";
                case 2:
                case 3:
                case 4: return $"{count} шага";
                default: return $"{count} шагов";
            }
        }

        /// <summary>
        /// Строка по ключу — но ЧЕРЕЗ <c>HasKey</c>, а не напрямую. Прямой
        /// <c>Localization.Get</c> на пропавшем ключе пишет ошибку в консоль, и отчёт-карта
        /// начал бы выглядеть как сломанный инструмент. Ругаться на локализацию —
        /// работа <see cref="LocalizationAudit"/>, у него для этого весь проект под рукой.
        /// </summary>
        private static string Loc(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "без ключа имени";
            return Utility.Services.Localization.Localization.HasKey(key)
                ? Utility.Services.Localization.Localization.Get(key)
                : $"ключа «{key}» нет в таблице";
        }

        /// <summary>
        /// Тот же приём, что в <see cref="LocalizationAudit"/>: ищем <c>t:ScriptableObject</c>
        /// и фильтруем типом в коде, чтобы не зависеть от состояния индекса поиска.
        /// </summary>
        private static List<Book> LoadAllBooks()
            => AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" })
                            .Select(AssetDatabase.GUIDToAssetPath)
                            .Distinct()
                            .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>)
                            .OfType<Book>()
                            .ToList();
    }
}
