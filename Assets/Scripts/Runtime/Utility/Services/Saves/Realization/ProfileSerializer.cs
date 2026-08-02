using System;
using UnityEngine;

namespace Utility.Services.Saves
{
    /// <summary>
    /// ПРОФИЛЬ ↔ JSON. Единственное место, где проект знает, каким именно сериализатором
    /// пишется сейв: носители (<see cref="ISaveStorage"/>) возят строку и про её устройство
    /// не знают вовсе.
    ///
    /// ПОЧЕМУ <c>JsonUtility</c>, а не Newtonsoft и не свой формат:
    ///
    /// 1. Он ЕСТЬ. <c>com.unity.modules.jsonserialize</c> уже в манифесте, Newtonsoft —
    ///    нет, и тащить пакет ради одного файла значит увеличить билд и завести
    ///    зависимость, которую придётся обновлять вместе с Unity.
    /// 2. Он РАБОТАЕТ НА WebGL. Это нативный код Unity, а не рефлексия: IL2CPP-стриппинг
    ///    (у проекта Managed Stripping = Low, но на релизе его поднимут) ему не мешает,
    ///    AOT-исключений он не даёт. У рефлексивных сериализаторов на IL2CPP это классика:
    ///    падает не в редакторе, а в собранном билде у игрока.
    /// 3. Он ДИФФИТСЯ И ЧИТАЕТСЯ. Сейв в баг-репорте видно глазом.
    ///
    /// ЧЕГО ОН НЕ УМЕЕТ и как это обойдено в <see cref="PlayerProfile"/>:
    ///  — СЛОВАРЕЙ НЕТ. Значит «уровень → результат» пишется массивом
    ///    <c>[Serializable]</c>-структур, а не <c>Dictionary</c>. Дороже на чтение,
    ///    зато никогда не молчаливая пустота.
    ///  — ПОЛИМОРФИЗМА НЕТ: поле базового типа сериализуется как базовый тип, наследник
    ///    теряется молча. Значит в профиле нет базовых классов — только конкретные типы.
    ///  — <c>null</c> У СТРОК НЕТ: он читается как <c>""</c>. Значит «значения нет»
    ///    кодируется пустой строкой, и проверка везде одна — <c>string.IsNullOrEmpty</c>.
    /// Эти три ограничения не «выстрелят позже» ровно до тех пор, пока структура профиля
    /// им соответствует, — поэтому они записаны здесь, а не в чьей-то голове.
    /// </summary>
    public static class ProfileSerializer
    {
        /// <summary>
        /// Огрызок профиля ради одного поля. Нужен, чтобы у сейва, который НЕ РАЗБИРАЕТСЯ
        /// целиком, всё равно можно было спросить версию: в логе «битый сейв v3»
        /// отвечает на вопрос «чей это сейв», а «битый сейв» — нет.
        /// Он же — точка, куда вешается миграция СЫРОГО json, если когда-нибудь
        /// поле сменит тип (тогда полный разбор старого сейва будет падать по определению).
        /// </summary>
        [Serializable]
        private class VersionProbe
        {
            public int Version;
        }

        public static string ToJson(PlayerProfile profile)
        {
            if (profile == null)
                return string.Empty;
            profile.Version = PlayerProfile.CURRENT_VERSION;
            return JsonUtility.ToJson(profile);
        }

        /// <summary>
        /// Разобрать сейв. <c>false</c> — читать нечего или прочитать не вышло; в обоих
        /// случаях вызывающий обязан взять новый профиль, а НЕ уронить игру.
        ///
        /// Битый сейв — это не «не должно случаться»: он лежит в <c>localStorage</c> браузера,
        /// правится из консоли за десять секунд, переживает откат версии игры и обрыв
        /// записи на закрытии вкладки. Единственная допустимая реакция — заметная запись
        /// в лог и новый профиль.
        /// </summary>
        public static bool TryFromJson(string json, out PlayerProfile profile)
        {
            profile = null;

            if (string.IsNullOrWhiteSpace(json))
                return false;

            int probedVersion = ProbeVersion(json);

            PlayerProfile parsed;
            try
            {
                parsed = JsonUtility.FromJson<PlayerProfile>(json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Saves] СЕЙВ НЕ ЧИТАЕТСЯ (версия по пробе: {VersionText(probedVersion)}): " +
                               $"{exception.GetType().Name}: {exception.Message}. Откат к новому профилю.");
                return false;
            }

            if (parsed == null)
            {
                Debug.LogError($"[Saves] СЕЙВ ПУСТ ПОСЛЕ РАЗБОРА (версия по пробе: {VersionText(probedVersion)}). " +
                               "Откат к новому профилю.");
                return false;
            }

            if (parsed.Version <= 0)
            {
                // Ни один написанный нами сейв не может иметь версию 0: её ставит
                // конструктор. Значит это не наш формат — чужие данные под нашим ключом.
                Debug.LogError("[Saves] У СЕЙВА НЕТ ВЕРСИИ — это не формат профиля. Откат к новому профилю.");
                return false;
            }

            if (!ProfileMigration.TryMigrate(parsed, out string error))
            {
                Debug.LogError($"[Saves] СЕЙВ НЕ СМИГРИРОВАН: {error}. Откат к новому профилю.");
                return false;
            }

            parsed.Normalize();
            profile = parsed;
            return true;
        }

        /// <summary>Версия из сырого json. -1 — даже пробу разобрать не вышло.</summary>
        private static int ProbeVersion(string json)
        {
            try
            {
                VersionProbe probe = JsonUtility.FromJson<VersionProbe>(json);
                return probe == null ? -1 : probe.Version;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static string VersionText(int version) => version < 0 ? "не определилась" : version.ToString();
    }
}
