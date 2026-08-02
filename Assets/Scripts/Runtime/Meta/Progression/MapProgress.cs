using System;
using Core.Battle;
using UnityEngine;
using Utility.Services.Saves;

namespace Meta
{
    /// <summary>
    /// КАРТА УЗЛОВ (docs/10 §13.4) и то, чем она заменила тупик <c>HomeController._configIndex</c>.
    ///
    /// ═══ ЧТО ИМЕННО БЫЛО СЛОМАНО ═══
    ///
    /// <c>_configIndex</c> рос на победе и упирался в последний уровень, НИКОГДА не
    /// сбрасываясь. Дойдя до конца, игрок получал бесконечный ре-ран последнего боя
    /// и ни одного способа вернуться назад: экрана выбора уровней не было, «нового круга»
    /// тоже. §13.4 закрывает это не кнопкой «пройти заново», а свойством карты —
    /// <b>узлы перепроходимы</b>, и место для фарма валюты появляется само.
    ///
    /// ═══ СТРУКТУРА ВЫВОДИТСЯ ИЗ ДАННЫХ, А НЕ ИЗ КОНСТАНТЫ 15 ═══
    ///
    /// В проекте есть правило «лимиты выводятся из данных»: число колб задаёт сцена,
    /// вместимость колбы — префаб. Здесь то же самое: <b>узлов ровно столько, сколько
    /// собрано <see cref="BattleConfig"/></b>. Сегодня их семь, по §13.4 планируется
    /// пятнадцать, и недостающие восемь — работа геймдизайнера, а не константа в коде.
    ///
    /// Боссы: §13.4 ставит их на узлы 5, 10 и 15, то есть НА ПОСЛЕДНИЙ УЗЕЛ КАЖДОЙ
    /// ГЛАВЫ при трёх главах. Это правило и записано (<see cref="IsBoss"/>); при
    /// пятнадцати узлах оно даёт ровно 5/10/15, при семи — 3/5/7, при любом другом
    /// числе — что-то осмысленное. Хардкод «5, 10, 15» при семи уровнях означал бы
    /// боссов, до которых нельзя дойти.
    ///
    /// ⚠️ Число глав (три) — единственная величина, которую вывести не из чего:
    /// главы завязаны на трёх героев (§13.4), а книг в проекте четыре, из них две —
    /// заглушки. Когда у <see cref="BattleConfig"/> появится собственный признак
    /// «босс» (это данные, а не закон), <see cref="IsBoss"/> обязан читать его,
    /// а не считать четверти.
    /// </summary>
    public sealed class MapProgress
    {
        /// <summary>
        /// Глав на карте (docs/10 §13.4: 1–5, 6–10, 11–15). Ровно столько же
        /// контрольных точек-боссов и столько же ступеней открытия героев (§15.4).
        /// </summary>
        public const int DESIGN_CHAPTERS = 3;

        /// <summary>Пройден ещё один узел. Аргумент — индекс узла.</summary>
        public event Action<int> OnNodeCleared;

        /// <summary>Пройден босс главы. Аргумент — номер главы, считая с единицы (§15.4: анлок героя).</summary>
        public event Action<int> OnChapterCleared;

        private readonly BattleConfig[] _configs;

        public MapProgress(BattleConfig[] configs)
        {
            _configs = configs ?? Array.Empty<BattleConfig>();

            int clamped = Mathf.Clamp(Saves.Profile.ClearedNodes, 0, NodeCount);
            if (clamped != Saves.Profile.ClearedNodes)
            {
                Debug.LogWarning($"[Мета] В сейве пройдено узлов {Saves.Profile.ClearedNodes}, " +
                                 $"а в сборке их {NodeCount}. Ставим {clamped}.");
                // Починку возвращаем в профиль, иначе она живёт только до конца сессии.
                // Записи на диск здесь НЕТ намеренно: старт игры — не точка сохранения,
                // а исправленное значение уедет при первом же настоящем сохранении.
                Saves.Profile.ClearedNodes = clamped;
            }
        }

        /// <summary>Узлов на карте. Выведено из числа собранных уровней, см. комментарий к классу.</summary>
        public int NodeCount => _configs.Length;

        /// <summary>Глав. Меньше трёх только на вырожденной карте из одного-двух узлов.</summary>
        public int ChapterCount => Mathf.Clamp(NodeCount, 0, DESIGN_CHAPTERS);

        /// <summary>Сколько узлов пройдено. Пройденное — всегда префикс: карта линейна.</summary>
        public int ClearedNodes => Mathf.Clamp(Saves.Profile.ClearedNodes, 0, NodeCount);

        /// <summary>Сколько узлов доступно: все пройденные плюс следующий.</summary>
        public int UnlockedNodes => Mathf.Min(ClearedNodes + 1, NodeCount);

        /// <summary>
        /// Узел, который карта предлагает по умолчанию, — первый непройденный.
        /// На добитой карте это последний узел: перепроходить его можно, а идти дальше
        /// некуда. Ровно то, что раньше делал <c>_configIndex</c>, но без тупика.
        /// </summary>
        public int NextNode => NodeCount == 0 ? 0 : Mathf.Min(ClearedNodes, NodeCount - 1);

        public bool IsValidNode(int node) => node >= 0 && node < NodeCount;

        public bool IsUnlocked(int node) => IsValidNode(node) && node < UnlockedNodes;

        public bool IsCleared(int node) => IsValidNode(node) && node < ClearedNodes;

        public BattleConfig ConfigOf(int node) => IsValidNode(node) ? _configs[node] : null;

        /// <summary>
        /// Глава узла, считая с единицы. Границы глав — четверти карты, обоснование
        /// в комментарии к классу.
        /// </summary>
        public int ChapterOf(int node)
        {
            if (!IsValidNode(node) || ChapterCount == 0)
                return 0;

            for (int chapter = 1; chapter <= ChapterCount; chapter++)
                if (node < LastNodeOfChapter(chapter) + 1)
                    return chapter;

            return ChapterCount;
        }

        /// <summary>Босс ли этот узел. Босс — последний узел главы (docs/10 §13.4).</summary>
        public bool IsBoss(int node)
        {
            if (!IsValidNode(node))
                return false;

            for (int chapter = 1; chapter <= ChapterCount; chapter++)
                if (LastNodeOfChapter(chapter) == node)
                    return true;

            return false;
        }

        /// <summary>
        /// Индекс последнего узла главы. Округление ВВЕРХ: при 15 узлах даёт 5/10/15
        /// (индексы 4/9/14), как в §13.4, при 7 — 3/5/7. Первая глава при этом чуть
        /// длиннее последних, что для обучающей главы скорее плюс.
        /// </summary>
        public int LastNodeOfChapter(int chapter)
        {
            if (chapter < 1 || ChapterCount == 0)
                return -1;

            chapter = Mathf.Min(chapter, ChapterCount);
            int lastCount = (NodeCount * chapter + ChapterCount - 1) / ChapterCount;
            return Mathf.Clamp(lastCount, 1, NodeCount) - 1;
        }

        /// <summary>
        /// ПОБЕДА НА УЗЛЕ — единственное место, где двигается прогресс карты.
        ///
        /// Возвращает <paramref name="firstClear"/>: узел пройден ВПЕРВЫЕ. По §15.3
        /// перепрохождение платит 40% от первой награды — само число живёт в контенте,
        /// а здесь только факт, из которого оно считается.
        ///
        /// ТОЧКА СОХРАНЕНИЯ. Пишем только при настоящем сдвиге прогресса: перепрохождение
        /// уже пройденного узла ничего в карте не меняет, и платить за это записью
        /// в IndexedDB не за что (награду сохранит тот, кто её начислит).
        /// </summary>
        public bool TryRegisterClear(int node, out bool firstClear)
        {
            firstClear = false;
            if (!IsValidNode(node))
                return false;

            firstClear = node >= ClearedNodes;
            if (firstClear)
            {
                Saves.Profile.ClearedNodes = Mathf.Min(node + 1, NodeCount);
                Saves.RequestSave($"узел {node + 1} пройден");
            }

            OnNodeCleared?.Invoke(node);

            if (firstClear && IsBoss(node))
                OnChapterCleared?.Invoke(ChapterOf(node));

            return true;
        }

        /// <summary>Отписка — по правилу проекта: событие, у которого не осталось хозяина, зануляется.</summary>
        public void Exit()
        {
            OnNodeCleared = null;
            OnChapterCleared = null;
        }
    }
}
