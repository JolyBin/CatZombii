// =====================================================================================
// ВРЕМЕННЫЙ ИЗМЕРИТЕЛЬНЫЙ ИНСТРУМЕНТ — Шаг 0 из docs/10-progression-design.md, §8.
//
// Меряет две разные вещи, и их важно не перепутать:
//
//   1. ТАКТ СХЛОПЫВАНИЯ В СЕКУНДАХ — сколько времени по настенным часам игрок тратит
//      на сбор четырёх одинаковых элементов. Нужен для гейтов docs/09 и для сверки
//      с кулдаунами, которые пока записаны в миллисекундах.
//
//   2. ПЕРЕЛИВОВ НА ОДНО СХЛОПЫВАНИЕ — конверсионная константа для пошагового режима
//      (§0.2, принят 01.08.2026). После перехода на пошаговость единица времени мира —
//      это ПЕРЕЛИВ, а не схлопывание: один перелив = один такт мира. Схлопывание стоит
//      нескольких переливов, поэтому кулдауны врагов надо переводить в такты через это
//      число, а не через такт схлопывания в секундах. Именно эта константа и была
//      неизвестна, из-за чего в §0.2 все враги «слились» в один такт.
//
// Не каждый перелив ведёт к схлопыванию, и часть переливов холостая (игрок раскладывает),
// поэтому по переливам считается и среднее, и медиана — распределение перекошено.
//
// КАК УДАЛИТЬ ПОСЛЕ ЗАМЕРА (2 шага, ничего больше не затронуто):
//   1. Удалить этот файл (и .meta).
//   2. Убрать в Assets/Scripts/Runtime/Flask/FlaskController.cs строки, помеченные
//      комментарием "TactMeter (временный замер, Шаг 0)" — их четыре плюс один using.
//
// Это не фича: ни одного serialized-поля, ни одного объекта в сцене, ни одной
// зависимости на инструмент со стороны игровой логики. Точки входа — существующие
// события FlaskController.OnFlaskFull (схлопывание) и FlaskController.MoveCommand (перелив).
// =====================================================================================

using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Utility.Diagnostics
{
    public static class TactMeter
    {
        private const string TAG = "<b>[TactMeter]</b> ";
        private const float PARTY_GATE_SECONDS = 300f; // docs/09: партия <= 5 минут
        private const float SESSION_GATE_SECONDS = 480f; // docs/09: сессия-заход >= 8 минут

        // --- Почему Time.unscaledTime, а не Time.time ---------------------------------
        // Меряется ТЕМП ЖИВОГО ЧЕЛОВЕКА: сколько секунд по настенным часам он тратит на
        // то, чтобы собрать четыре одинаковых элемента. Это величина из мира игрока,
        // а не из мира симуляции. Time.time умножен на Time.timeScale, поэтому любая
        // пауза (timeScale = 0) заморозила бы счётчик и «подарила» игроку время,
        // которое он на самом деле потратил, — такт вышел бы заниженным, а именно
        // занижение такта в 2-3 раза и есть та ошибка, которую этот замер ловит.
        // Ускорение/замедление времени исказило бы результат симметрично.
        // Сейчас timeScale в проекте нигде не трогается, но инструмент должен пережить
        // появление паузы, не соврав. Отсюда unscaledTime — единственный корректный
        // источник для этой метрики.
        // ------------------------------------------------------------------------------
        private static float Now => Time.unscaledTime;

        private static bool _battleRunning;
        private static float _battleStartTime;
        private static float _lastCollapseTime;

        // _intervals[0] — от старта боя до первого схлопывания, дальше — между схлопываниями.
        private static readonly List<float> _intervals = new List<float>(64);

        // --- Переливы ------------------------------------------------------------------
        // _moves — все переливы за партию, включая холостые (перекладывание без схлопывания).
        // _movesPerCollapse[i] — сколько переливов стоило i-е схлопывание (первое включает
        //   разбор стартовой раскладки, поэтому считается отдельно, как и интервал).
        // _movesInSegment — переливы, накопленные после последнего схлопывания.
        private static int _moves;
        private static int _movesInSegment;
        private static readonly List<int> _movesPerCollapse = new List<int>(64);

        // Порядок событий в FlaskController.MoveBall(): сначала PushElement(), который
        // синхронно поднимает OnFlaskFull (схлопывание), и только потом MoveCommand.
        // То есть перелив, вызвавший схлопывание, приходит ПОСЛЕ него. Флаг помечает,
        // что этот перелив уже засчитан в закрывшийся отрезок, и его не надо класть
        // в начало следующего — иначе каждый отрезок съезжал бы на один перелив.
        private static bool _causingMovePending;

        private static string _outcome;
        private static int _partyNumber;
        private static float _sessionSeconds;
        private static int _sessionMoves;
        // Отрезки всех партий сессии, без первых схлопываний — по ним считается итоговая
        // константа конверсии. Первое схлопывание партии сюда не попадает: в нём сидит
        // стартовая раскладка, которую игрок не собирал.
        private static readonly List<int> _sessionSegments = new List<int>(256);
        private static string _levelLabel = "?";
        private static Watcher _watcher;

        // Статика переживает вход в Play Mode, если в проекте выключен Domain Reload.
        // Сбрасываем явно, чтобы замер всегда начинался с чистого листа.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _battleRunning = false;
            _battleStartTime = 0f;
            _lastCollapseTime = 0f;
            _intervals.Clear();
            _moves = 0;
            _movesInSegment = 0;
            _movesPerCollapse.Clear();
            _causingMovePending = false;
            _outcome = null;
            _partyNumber = 0;
            _sessionSeconds = 0f;
            _sessionMoves = 0;
            _sessionSegments.Clear();
            _levelLabel = "?";
            _watcher = null;
        }

        /// <summary>Вызывается из FlaskController.Init() — момент старта партии (боя).</summary>
        public static void BeginBattle()
        {
            if (_battleRunning)
                EndBattle(); // подстраховка: предыдущая партия не закрылась

            EnsureWatcher();

            _battleRunning = true;
            _partyNumber++;
            _battleStartTime = Now;
            _lastCollapseTime = Now;
            _intervals.Clear();
            _moves = 0;
            _movesInSegment = 0;
            _movesPerCollapse.Clear();
            _causingMovePending = false;
            _outcome = null;
            _levelLabel = ReadCurrentLevelLabel();

            Debug.Log(TAG + $"=== ПАРТИЯ #{_partyNumber} НАЧАЛАСЬ (уровень {_levelLabel}). " +
                            "Замер такта и переливов пошёл. Просто играй. ===");
        }

        /// <summary>
        /// Вызывается из FlaskController по событию MoveCommand — игрок перелил шарик.
        /// В пошаговом режиме это и есть один такт мира.
        /// </summary>
        public static void RegisterMove()
        {
            if (!_battleRunning)
                return;

            _moves++;

            if (_causingMovePending)
            {
                // Этот перелив только что вызвал схлопывание и уже учтён в его отрезке.
                _causingMovePending = false;
                return;
            }

            _movesInSegment++;
        }

        /// <summary>Вызывается из FlaskController по событию OnFlaskFull — колба схлопнулась.</summary>
        public static void RegisterCollapse()
        {
            if (!_battleRunning)
                return;

            float now = Now;
            float interval = now - _lastCollapseTime;
            _lastCollapseTime = now;
            _intervals.Add(interval);

            // +1 — перелив, который прямо сейчас вызвал это схлопывание: его MoveCommand
            // придёт следующей строкой кода, поэтому в _movesInSegment его ещё нет.
            int segment = _movesInSegment + 1;
            _movesPerCollapse.Add(segment);
            _movesInSegment = 0;
            _causingMovePending = true;

            float running = Mean(_intervals, 0);
            int n = _intervals.Count;

            if (n == 1)
            {
                Debug.Log(TAG + $"Схлопывание #1: {F(interval)} с от старта боя, переливов {segment} " +
                                $"| среднее {F(running)} с");
            }
            else
            {
                Debug.Log(TAG + $"Схлопывание #{n}: +{F(interval)} с (интервал), переливов {segment} " +
                                $"| среднее {F(running)} с и {F(MeanI(_movesPerCollapse, 0))} перелива " +
                                $"| всего с начала боя {F(now - _battleStartTime)} с, переливов {_moves + 1}");
            }
        }

        /// <summary>Вызывается из FlaskController.Exit() — партия закончилась любым способом.</summary>
        public static void EndBattle()
        {
            if (!_battleRunning)
                return;

            _battleRunning = false;

            // Страховка на случай, если партия оборвалась между схлопыванием и его переливом:
            // перелив был, MoveCommand по нему не пришёл — он уже учтён в отрезке, добираем
            // только общий счётчик, чтобы «секунд на перелив» не соврали.
            if (_causingMovePending)
            {
                _causingMovePending = false;
                _moves++;
            }

            float party = Now - _battleStartTime;
            _sessionSeconds += party;
            _sessionMoves += _moves;

            int n = _intervals.Count;
            for (int i = 1; i < _movesPerCollapse.Count; i++)
                _sessionSegments.Add(_movesPerCollapse[i]);

            string outcome = string.IsNullOrEmpty(_outcome) ? "ВЫХОД (кнопка домой / не доиграно)" : _outcome;

            var sb = new StringBuilder();
            sb.AppendLine(TAG + $"===== ИТОГ ПАРТИИ #{_partyNumber} (уровень {_levelLabel}) =====");
            sb.AppendLine($"Исход: {outcome}");
            sb.AppendLine($"Длина партии: {F(party)} с ({Clock(party)}) | гейт «партия ≤ 5 мин»: " +
                          (party <= PARTY_GATE_SECONDS ? "ПРОЙДЕН" : "ПРОВАЛЕН"));
            sb.AppendLine($"Схлопываний за партию: {n}");
            sb.AppendLine($"Переливов за партию: {_moves}" +
                          (_moves > 0 ? $" | секунд на один перелив: {F(party / _moves)} с" : ""));

            if (n == 0)
            {
                sb.AppendLine("Ни одного схлопывания — ни такт, ни конверсия не измерены.");
            }
            else
            {
                sb.AppendLine("--- ВРЕМЯ (секунды) -----------------------------------------");
                sb.AppendLine($"Первое схлопывание (вход в игру): {F(_intervals[0])} с");
                sb.AppendLine($"ТАКТ, среднее по всем: {F(Mean(_intervals, 0))} с");
                if (n > 1)
                    sb.AppendLine($"ТАКТ устоявшийся, без первого: {F(Mean(_intervals, 1))} с   [ЭТО ЧИСЛО НУЖНО ДЛЯ ВЁРСТКИ ВОЛН]");
                sb.AppendLine($"Медиана интервалов: {F(Median(_intervals))} с");
                sb.AppendLine($"Мин / макс интервал: {F(Min(_intervals))} / {F(Max(_intervals))} с");
                sb.AppendLine($"Все интервалы, с: {Join(_intervals)}");

                sb.AppendLine("--- ПОШАГОВОСТЬ (переливы = такты мира, §0.2) ---------------");
                sb.AppendLine($"Первое схлопывание (вход в игру): переливов {_movesPerCollapse[0]}");
                sb.AppendLine($"ПЕРЕЛИВОВ НА СХЛОПЫВАНИЕ, среднее по всем: {F(MeanI(_movesPerCollapse, 0))}");
                if (n > 1)
                {
                    sb.AppendLine($"ПЕРЕЛИВОВ НА СХЛОПЫВАНИЕ, без первого: {F(MeanI(_movesPerCollapse, 1))}   [ЭТО ЧИСЛО НУЖНО ДЛЯ ВЁРСТКИ ВОЛН — ТАКТ МИРА В ПЕРЕЛИВАХ]");
                    sb.AppendLine($"МЕДИАНА переливов на схлопывание, без первого: {F(MedianI(_movesPerCollapse, 1))}   [ЭТО ЧИСЛО НУЖНО ДЛЯ ВЁРСТКИ ВОЛН — ТАКТ МИРА В ПЕРЕЛИВАХ]");
                }
                sb.AppendLine($"Медиана переливов по всем: {F(MedianI(_movesPerCollapse, 0))}");
                sb.AppendLine($"Мин / макс переливов на схлопывание: {MinI(_movesPerCollapse)} / {MaxI(_movesPerCollapse)}");
                sb.AppendLine($"Переливов между схлопываниями: {JoinI(_movesPerCollapse)}");
                sb.AppendLine($"Переливов после последнего схлопывания (холостой хвост): {_movesInSegment}");
            }

            sb.AppendLine($"Сессия суммарно: партий {_partyNumber}, {Clock(_sessionSeconds)}, " +
                          $"переливов {_sessionMoves} | гейт «сессия ≥ 8 мин»: " +
                          (_sessionSeconds >= SESSION_GATE_SECONDS ? "ПРОЙДЕН" : "пока нет"));
            if (_sessionSegments.Count > 0)
                sb.AppendLine($"Сессия, переливов на схлопывание (без первых; схлопываний: {_sessionSegments.Count}): " +
                              $"среднее {F(MeanI(_sessionSegments, 0))}, медиана {F(MedianI(_sessionSegments, 0))}   " +
                              "[ИТОГОВАЯ КОНВЕРСИЯ ЗА СЕССИЮ]");
            sb.Append(TAG + "=========================================");

            Debug.Log(sb.ToString());
        }

        internal static void NoteOutcome(string outcome)
        {
            if (_battleRunning && string.IsNullOrEmpty(_outcome))
                _outcome = outcome;
        }

        // ---------------------------------------------------------------------------
        // Определение исхода партии и аварийное закрытие замера.
        //
        // Победа/поражение живут в BattleController/StepsController, которые по границам
        // задачи трогать нельзя. Поэтому исход снимается снаружи и пассивно: окна
        // UIWinWindow / UILoseWindow скрываются не через SetActive, а через Canvas.enabled
        // (см. UIWindow.Show/Hide), так что достаточно смотреть на флаг канваса.
        // Ни строчки в чужих файлах.
        // ---------------------------------------------------------------------------
        private static void EnsureWatcher()
        {
            if (_watcher != null)
                return;

            // Объект намеренно виден в Hierarchy: пока он там есть — замер включён.
            var go = new GameObject("[TactMeter]");
            Object.DontDestroyOnLoad(go);
            _watcher = go.AddComponent<Watcher>();
        }

        private sealed class Watcher : MonoBehaviour
        {
            private Canvas _winCanvas;
            private Canvas _loseCanvas;
            private bool _resolved;

            private void Awake()
            {
                var win = FindAnyObjectByType<Core.Steps.UIWinWindow>(FindObjectsInactive.Include);
                var lose = FindAnyObjectByType<Core.Steps.UILoseWindow>(FindObjectsInactive.Include);
                if (win != null) _winCanvas = win.GetComponent<Canvas>();
                if (lose != null) _loseCanvas = lose.GetComponent<Canvas>();
            }

            private void LateUpdate()
            {
                if (!_battleRunning)
                {
                    _resolved = false;
                    return;
                }

                if (_resolved)
                    return;

                if (_winCanvas != null && _winCanvas.enabled)
                {
                    _resolved = true;
                    NoteOutcome("ПОБЕДА");
                    Debug.Log(TAG + $"Победа на {F(Now - _battleStartTime)} с партии. " +
                                    "Итог напечатается после кнопки «продолжить».");
                }
                else if (_loseCanvas != null && _loseCanvas.enabled)
                {
                    _resolved = true;
                    NoteOutcome("ПОРАЖЕНИЕ");
                    Debug.Log(TAG + $"Поражение на {F(Now - _battleStartTime)} с партии. " +
                                    "Итог напечатается после кнопки «продолжить».");
                }
            }

            // Выход из Play Mode / закрытие вкладки посреди боя — не теряем замер.
            private void OnDestroy()
            {
                if (_battleRunning)
                {
                    NoteOutcome("ПРЕРВАНО (выход из Play Mode)");
                    EndBattle();
                }
                _watcher = null;
            }

            private void OnApplicationQuit()
            {
                if (_battleRunning)
                {
                    NoteOutcome("ПРЕРВАНО (выход из игры)");
                    EndBattle();
                }
            }
        }

        // --- Номер уровня для шапки лога ------------------------------------------------
        // HomeController._configIndex приватен, а сам HomeController — не MonoBehaviour,
        // достать его из сцены нельзя. Номер уровня уже нарисован на UIHomeWindow, оттуда
        // и читаем. Рефлексия здесь дешевле, чем правка чужого файла ради подписи в логе;
        // если сорвётся — просто «?», на замер это не влияет.
        private static string ReadCurrentLevelLabel()
        {
            try
            {
                var home = Object.FindAnyObjectByType<Meta.UI.UIHomeWindow>(FindObjectsInactive.Include);
                if (home == null)
                    return "?";

                var field = typeof(Meta.UI.UIHomeWindow).GetField(
                    "_currentLevelText", BindingFlags.Instance | BindingFlags.NonPublic);
                var text = field?.GetValue(home) as TMPro.TextMeshProUGUI;
                if (text == null || string.IsNullOrEmpty(text.text))
                    return "?";

                // "Current Level: 3" -> "3"
                int colon = text.text.LastIndexOf(':');
                return colon >= 0 ? text.text.Substring(colon + 1).Trim() : text.text.Trim();
            }
            catch
            {
                return "?";
            }
        }

        // --- Мелкая арифметика и форматирование ------------------------------------------
        private static string F(float seconds) =>
            seconds.ToString("F1", CultureInfo.InvariantCulture);

        private static string Clock(float seconds)
        {
            int total = Mathf.RoundToInt(seconds);
            return $"{total / 60} мин {total % 60:00} с";
        }

        private static string Join(List<float> values)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append("; ");
                sb.Append(F(values[i]));
            }
            return sb.ToString();
        }

        private static float Mean(List<float> values, int skipFirst)
        {
            float sum = 0f;
            int count = 0;
            for (int i = skipFirst; i < values.Count; i++)
            {
                sum += values[i];
                count++;
            }
            return count == 0 ? 0f : sum / count;
        }

        private static float Median(List<float> values)
        {
            if (values.Count == 0)
                return 0f;
            var copy = new List<float>(values);
            copy.Sort();
            int mid = copy.Count / 2;
            return copy.Count % 2 == 1 ? copy[mid] : (copy[mid - 1] + copy[mid]) * 0.5f;
        }

        // --- То же самое для переливов (счётные величины, отдельные перегрузки) ----------
        private static string JoinI(List<int> values)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append("; ");
                sb.Append(values[i].ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static float MeanI(List<int> values, int skipFirst)
        {
            float sum = 0f;
            int count = 0;
            for (int i = skipFirst; i < values.Count; i++)
            {
                sum += values[i];
                count++;
            }
            return count == 0 ? 0f : sum / count;
        }

        private static float MedianI(List<int> values, int skipFirst)
        {
            if (values.Count <= skipFirst)
                return 0f;
            var copy = new List<int>(values.Count - skipFirst);
            for (int i = skipFirst; i < values.Count; i++)
                copy.Add(values[i]);
            copy.Sort();
            int mid = copy.Count / 2;
            return copy.Count % 2 == 1 ? copy[mid] : (copy[mid - 1] + copy[mid]) * 0.5f;
        }

        private static int MinI(List<int> values)
        {
            if (values.Count == 0)
                return 0;
            int m = int.MaxValue;
            for (int i = 0; i < values.Count; i++)
                if (values[i] < m) m = values[i];
            return m;
        }

        private static int MaxI(List<int> values)
        {
            if (values.Count == 0)
                return 0;
            int m = int.MinValue;
            for (int i = 0; i < values.Count; i++)
                if (values[i] > m) m = values[i];
            return m;
        }

        private static float Min(List<float> values)
        {
            float m = float.MaxValue;
            for (int i = 0; i < values.Count; i++)
                if (values[i] < m) m = values[i];
            return values.Count == 0 ? 0f : m;
        }

        private static float Max(List<float> values)
        {
            float m = float.MinValue;
            for (int i = 0; i < values.Count; i++)
                if (values[i] > m) m = values[i];
            return values.Count == 0 ? 0f : m;
        }
    }
}
