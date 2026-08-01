// =====================================================================================
// ВРЕМЕННЫЙ ИЗМЕРИТЕЛЬНЫЙ ИНСТРУМЕНТ — Шаг 0 из docs/10-progression-design.md, §8.
//
// Задача: измерить РЕАЛЬНЫЙ такт схлопывания колбы у живого игрока. Из этой константы
// выводятся длительность волн, суммарные HP, цены и вся экономика. Заодно меряется
// длина партии целиком — для платформенного гейта «партия <= 5 минут» (docs/09).
//
// КАК УДАЛИТЬ ПОСЛЕ ЗАМЕРА (2 шага, ничего больше не затронуто):
//   1. Удалить этот файл (и .meta).
//   2. Убрать в Assets/Scripts/Runtime/Flask/FlaskController.cs строки, помеченные
//      комментарием "TactMeter (временный замер, Шаг 0)" — их три плюс один using.
//
// Это не фича: ни одного serialized-поля, ни одного объекта в сцене, ни одной
// зависимости на инструмент со стороны игровой логики. Точка входа — существующее
// событие FlaskController.OnFlaskFull.
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

        private static string _outcome;
        private static int _partyNumber;
        private static float _sessionSeconds;
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
            _outcome = null;
            _partyNumber = 0;
            _sessionSeconds = 0f;
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
            _outcome = null;
            _levelLabel = ReadCurrentLevelLabel();

            Debug.Log(TAG + $"=== ПАРТИЯ #{_partyNumber} НАЧАЛАСЬ (уровень {_levelLabel}). " +
                            "Замер такта пошёл. Просто играй. ===");
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

            float running = Mean(_intervals, 0);
            int n = _intervals.Count;

            if (n == 1)
            {
                Debug.Log(TAG + $"Схлопывание #1: {F(interval)} с от старта боя " +
                                $"| среднее {F(running)} с");
            }
            else
            {
                Debug.Log(TAG + $"Схлопывание #{n}: +{F(interval)} с (интервал) " +
                                $"| среднее {F(running)} с | всего с начала боя {F(now - _battleStartTime)} с");
            }
        }

        /// <summary>Вызывается из FlaskController.Exit() — партия закончилась любым способом.</summary>
        public static void EndBattle()
        {
            if (!_battleRunning)
                return;

            _battleRunning = false;

            float party = Now - _battleStartTime;
            _sessionSeconds += party;

            int n = _intervals.Count;
            string outcome = string.IsNullOrEmpty(_outcome) ? "ВЫХОД (кнопка домой / не доиграно)" : _outcome;

            var sb = new StringBuilder();
            sb.AppendLine(TAG + $"===== ИТОГ ПАРТИИ #{_partyNumber} (уровень {_levelLabel}) =====");
            sb.AppendLine($"Исход: {outcome}");
            sb.AppendLine($"Длина партии: {F(party)} с ({Clock(party)}) | гейт «партия ≤ 5 мин»: " +
                          (party <= PARTY_GATE_SECONDS ? "ПРОЙДЕН" : "ПРОВАЛЕН"));
            sb.AppendLine($"Схлопываний за партию: {n}");

            if (n == 0)
            {
                sb.AppendLine("Ни одного схлопывания — такт не измерен.");
            }
            else
            {
                sb.AppendLine($"Первое схлопывание (вход в игру): {F(_intervals[0])} с");
                sb.AppendLine($"ТАКТ, среднее по всем: {F(Mean(_intervals, 0))} с");
                if (n > 1)
                    sb.AppendLine($"ТАКТ устоявшийся, без первого: {F(Mean(_intervals, 1))} с   [ЭТО ЧИСЛО НУЖНО ДЛЯ ВЁРСТКИ ВОЛН]");
                sb.AppendLine($"Медиана интервалов: {F(Median(_intervals))} с");
                sb.AppendLine($"Мин / макс интервал: {F(Min(_intervals))} / {F(Max(_intervals))} с");
                sb.AppendLine($"Все интервалы, с: {Join(_intervals)}");
            }

            sb.AppendLine($"Сессия суммарно: партий {_partyNumber}, {Clock(_sessionSeconds)} | " +
                          $"гейт «сессия ≥ 8 мин»: " +
                          (_sessionSeconds >= SESSION_GATE_SECONDS ? "ПРОЙДЕН" : "пока нет"));
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
