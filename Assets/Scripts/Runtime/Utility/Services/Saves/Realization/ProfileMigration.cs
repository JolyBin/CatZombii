using UnityEngine;

namespace Utility.Services.Saves
{
    /// <summary>
    /// ПУТЬ ОТ СТАРОГО ФОРМАТА К ТЕКУЩЕМУ. Ступеньками по одной версии: v1→v2, v2→v3, …
    ///
    /// Почему ступеньками, а не «прочитать что получится и забыть». Прыжок «из любой версии
    /// сразу в текущую» пишется один раз, а потом обязан помнить все промежуточные формы —
    /// то есть превращается в кучу <c>if</c>, которую никто не в состоянии проверить.
    /// Ступенька же проверяется целиком: у неё один вход и один выход.
    ///
    /// Правило на будущее: МЕНЯЕШЬ СМЫСЛ ИЛИ ИМЯ ПОЛЯ — поднимаешь
    /// <see cref="PlayerProfile.CURRENT_VERSION"/> и дописываешь <c>case</c>
    /// в <see cref="TryMigrateStep"/>. ДОБАВЛЯЕШЬ новое поле — версию поднимать НЕ надо:
    /// <c>JsonUtility</c> оставит в нём значение по умолчанию, и это ровно то, что нужно
    /// (у старого игрока новой сущности и не было).
    ///
    /// Сегодня версия одна, и ступенек нет. Это не значит, что механизм лишний:
    /// его цена сейчас — двадцать строк, а его отсутствие в день первой смены формата
    /// стоит прогресса всех, кто уже играет.
    /// </summary>
    public static class ProfileMigration
    {
        /// <summary>Страховка от ступеньки, которая не двигает версию: вечный цикл на старте игры хуже потерянного сейва.</summary>
        private const int MAX_STEPS = 64;

        /// <summary>
        /// Поднять профиль до текущей версии. <c>false</c> — путь неизвестен; вызывающий
        /// обязан откатиться к новому профилю, а не пытаться играть на полупонятых данных.
        /// </summary>
        public static bool TryMigrate(PlayerProfile profile, out string error)
        {
            error = null;

            if (profile == null)
            {
                error = "профиль пуст";
                return false;
            }

            if (profile.Version > PlayerProfile.CURRENT_VERSION)
            {
                // Сейв из БОЛЕЕ НОВОЙ сборки. Живой сценарий — откат релиза на площадке.
                // Читать его «как получится» нельзя: незнакомые поля мы молча потеряем
                // и при следующем сохранении затрём. Честнее начать заново, чем испортить.
                error = $"сейв версии {profile.Version} новее, чем понимает игра ({PlayerProfile.CURRENT_VERSION})";
                return false;
            }

            int steps = 0;
            while (profile.Version < PlayerProfile.CURRENT_VERSION)
            {
                int from = profile.Version;

                if (!TryMigrateStep(profile, from, out error))
                    return false;

                if (profile.Version <= from || ++steps > MAX_STEPS)
                {
                    error = $"миграция с версии {from} не подняла версию";
                    return false;
                }

                Debug.Log($"[Saves] Сейв смигрирован: v{from} → v{profile.Version}.");
            }

            return true;
        }

        /// <summary>
        /// Одна ступенька: привести профиль версии <paramref name="from"/> к версии
        /// <c>from + 1</c> и записать новую версию в <see cref="PlayerProfile.Version"/>.
        /// </summary>
        private static bool TryMigrateStep(PlayerProfile profile, int from, out string error)
        {
            switch (from)
            {
                // Образец будущей ступеньки — оставлен намеренно, чтобы следующий формат
                // добавлялся дописыванием case'а, а не выдумыванием схемы с нуля:
                //
                // case 1:
                //     profile.Coins += profile.LegacyStars * 10;  // лапки стали монетами
                //     profile.Version = 2;
                //     error = null;
                //     return true;

                default:
                    error = $"нет миграции с версии {from} на {from + 1}";
                    return false;
            }
        }
    }
}
