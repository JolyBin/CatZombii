using Core.Battle;
using Core.Spells;
using Core.Steps;
using UnityEditor;
using UnityEngine;

namespace CatZombii.EditorTools
{
    /// <summary>
    /// ПОДСКАЗКИ В ИНСПЕКТОРЕ ДЛЯ ВСЕГО, ЧТО ИЗМЕРЯЕТСЯ В ТАКТАХ.
    ///
    /// Зачем. У каждой такой длительности сегодня ДВА поля: такты (основное) и наследие
    /// прототипа в миллисекундах или секундах (фолбэк). Заголовков и подсказок хватает,
    /// чтобы понять, куда писать, но не хватает, чтобы увидеть РЕЗУЛЬТАТ: пока в ассете
    /// стоят миллисекунды, действующее число прячется за <c>WorldClock.MILLISECONDS_PER_TACT</c>,
    /// а именно оно и сравнивается с docs/10 §13.7 («Тухлик — раз в 3 хода»).
    ///
    /// Поэтому под стандартным инспектором печатается одна строка: сколько ходов получилось
    /// и откуда это число взялось. Ничего не правит и ни на что не влияет — только показывает.
    ///
    /// ⚠️ Множитель конверсии — СТАРТОВОЕ ПРЕДПОЛОЖЕНИЕ, а не замер (docs/10 §8, шаг 0,
    /// вторая метрика ещё не снята). Значит и всё, что выведено из миллисекунд, — тоже
    /// предположение, и подсказка об этом говорит вслух.
    /// </summary>
    internal static class TactHelp
    {
        internal static void Draw(string what, int effectiveTacts, bool authoredInTacts, string legacyText)
        {
            if (effectiveTacts <= 0)
            {
                EditorGUILayout.HelpBox($"{what}: НЕ ЗАДАНО ни в тактах, ни в наследии — эффекта не будет.",
                                        MessageType.None);
                return;
            }

            if (authoredInTacts)
            {
                EditorGUILayout.HelpBox($"{what}: {effectiveTacts} — задано в ТАКТАХ, как и надо. " +
                                        "Множитель миллисекунд на это значение не влияет.",
                                        MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                $"{what}: {effectiveTacts} — ВЫВЕДЕНО из наследия ({legacyText}) делением на " +
                $"WorldClock.MILLISECONDS_PER_TACT = {WorldClock.MILLISECONDS_PER_TACT}.\n" +
                "Этот множитель — стартовое предположение, а не замер (docs/10 §8, шаг 0): после замера " +
                "«переливов на схлопывание» число здесь поедет само. Хочешь, чтобы оно было устойчивым, — " +
                "впиши такты в поле выше.",
                MessageType.Warning);
        }
    }

    [CustomEditor(typeof(UnitConfig))]
    internal sealed class UnitConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = (UnitConfig)target;
            TactHelp.Draw("Враг ходит раз в N ходов игрока, N",
                          config.AttackCooldownTacts,
                          config.IsCooldownAuthoredInTacts,
                          $"{config.AttackCooldown} мс");

            EditorGUILayout.LabelField("Ориентир docs/10 §13.7", "Тухлик 3 · Крыс 1 · Здоровяк 4 · Босс 2");
        }
    }

    [CustomEditor(typeof(BossSpellConfig))]
    internal sealed class BossSpellConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = (BossSpellConfig)target;
            EditorGUILayout.LabelField("Действующие значения в тактах", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Фаза 2 (ниже 70% HP)", $"раз в {config.TwoPhaseAttackCooldownTacts} ход(ов)");
            EditorGUILayout.LabelField("Фаза 3 (ниже 30% HP)", $"раз в {config.ThreePhaseAttackCooldownTacts} ход(ов)");
            EditorGUILayout.LabelField("Волна помощников", $"раз в {config.SpawnCooldownTacts} ход(ов) босса");
            EditorGUILayout.HelpBox("Фаза 1 задаётся кулдауном самого босса в его UnitConfig, а не здесь.",
                                    MessageType.None);
        }
    }

    [CustomEditor(typeof(FastAttackSpellConfig))]
    internal sealed class FastAttackSpellConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = (FastAttackSpellConfig)target;
            TactHelp.Draw("Цель пропустит ходов",
                          config.StunTacts,
                          config.IsStunAuthoredInTacts,
                          $"{config.StunTimer} с");
        }
    }

    [CustomEditor(typeof(RegenirationConfig))]
    internal sealed class RegenirationConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = (RegenirationConfig)target;
            if (config.HealCount <= 1)
            {
                EditorGUILayout.HelpBox("Тик один — лечение мгновенное, пауза между тиками не используется вовсе " +
                                        "(docs/10 §14.1: «мгновенное лечение — это RegenirationConfig с _count = 1»).",
                                        MessageType.None);
                return;
            }

            TactHelp.Draw("Пауза между тиками, ходов игрока",
                          config.IntervalTacts,
                          config.IsIntervalAuthoredInTacts,
                          "миллисекунды прототипа");
        }
    }
}
