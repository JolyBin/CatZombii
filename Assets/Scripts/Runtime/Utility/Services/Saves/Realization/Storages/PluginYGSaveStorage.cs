using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Utility.Services.Saves
{
    /// <summary>
    /// ЕДИНСТВЕННОЕ МЕСТО ВО ВСЁМ ПРОЕКТЕ, где сохранения знают про PluginYG.
    /// Тот же приём, что у <c>PluginYGLanguageSource</c>, и по той же причине —
    /// docs/09 (строки 226-229): в игровом коде ноль ссылок на SDK, иначе выход
    /// на мобилки и Steam дорожает. Удаление этого файла целиком не ломает сохранения:
    /// остаётся <see cref="PlayerPrefsSaveStorage"/>.
    ///
    /// ═══ ПРО ДВОЙНОЙ <c>#if</c> — ЭТО НЕ ПЕРЕСТРАХОВКА, А ФАКТ ПРОЕКТА НА 02.08.2026 ═══
    ///
    /// Дефайн <c>PLUGIN_YG_2</c> стоит (в том числе на Standalone, то есть и в редакторе),
    /// но МОДУЛЬ СОХРАНЕНИЙ PluginYG В ПРОЕКТ НЕ УСТАНОВЛЕН: папка
    /// <c>Assets/PluginYourGames/Modules/</c> ПУСТА, класса <c>SavesYG2</c> в проекте нет,
    /// а <c>YG2.SaveProgress()</c> и <c>YG2.saves</c> не существуют — единственное
    /// упоминание этого API (<c>EventsYG2.cs:202-206</c>) само закрыто дефайном
    /// <c>Storage_yg</c>, который выставляет модуль при установке.
    /// Guard только по <c>PLUGIN_YG_2</c> уронил бы компиляцию сегодня же.
    ///
    /// ═══ КАК ВКЛЮЧИТЬ, КОГДА ДОЙДУТ РУКИ ═══
    ///
    /// 1. PluginYG → Modules → установить модуль **Storage** (он же «Сохранения»).
    ///    Модуль сам добавит дефайн <c>Storage_yg</c> и создаст класс <c>SavesYG2</c>.
    /// 2. В <c>SavesYG2</c> добавить ОДНО поле:  <c>public string profileJson;</c>
    ///    Больше плагину о нашем профиле знать не нужно: наружу уезжает готовая строка,
    ///    а её формат — дело <see cref="ProfileSerializer"/>. Так формат сейва остаётся
    ///    общим для всех платформ, а платформенным остаётся только носитель.
    /// 3. Больше ничего: этот носитель уже зарегистрирован в <see cref="Saves"/> первым
    ///    и включится сам.
    ///
    /// ⚠️ Проверить после установки: включается облако — ПЕРЕЧИТАТЬ порядок носителей
    /// в <see cref="Saves"/>. Сегодня облако стоит первым и его ответ побеждает локальный;
    /// это правильно для площадки с одним аккаунтом на нескольких устройствах, но это
    /// продуктовое решение, а не техническое, и оно записано там одной видимой строкой.
    /// </summary>
    public class PluginYGSaveStorage : ISaveStorage
    {
        /// <summary>Сколько ждём инициализации SDK, прежде чем пойти читать локальный сейв.</summary>
        private const int SDK_WAIT_SECONDS = 5;

        public string Name => "Облако Яндекс.Игр (PluginYG)";

#if PLUGIN_YG_2 && Storage_yg
        public bool IsAvailable => true;
#else
        /// <summary>
        /// Носителя в этой сборке нет — он честно молчит, и весь прогресс живёт
        /// в <see cref="PlayerPrefsSaveStorage"/>. Это не ошибка и не деградация:
        /// это состояние проекта до установки модуля.
        /// </summary>
        public bool IsAvailable => false;
#endif

        public async UniTask<string> LoadAsync(CancellationToken token)
        {
#if PLUGIN_YG_2 && Storage_yg
            if (!await WaitForSdkAsync(token))
            {
                Debug.LogWarning($"[Saves] {Name}: SDK не поднялся за {SDK_WAIT_SECONDS} с — читаем локальный сейв.");
                return string.Empty;
            }

            try
            {
                // Плагин уже прочитал облако при инициализации: своего запроса не делаем,
                // просто берём поле. Строку положил туда SaveAsync — её формат плагину неизвестен.
                return YG.YG2.saves.profileJson;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Saves] {Name}: чтение не удалось ({exception.Message}).");
                return string.Empty;
            }
#else
            await UniTask.CompletedTask;
            token.ThrowIfCancellationRequested();
            return string.Empty;
#endif
        }

        public async UniTask<bool> SaveAsync(string json, CancellationToken token)
        {
#if PLUGIN_YG_2 && Storage_yg
            if (!await WaitForSdkAsync(token))
                return false;

            try
            {
                YG.YG2.saves.profileJson = json;
                // Плагин сам разбирается с частотным лимитом площадки на запись в облако.
                YG.YG2.SaveProgress();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Saves] {Name}: запись не удалась ({exception.Message}).");
                return false;
            }
#else
            await UniTask.CompletedTask;
            token.ThrowIfCancellationRequested();
            return false;
#endif
        }

        public async UniTask<bool> DeleteAsync(CancellationToken token)
        {
#if PLUGIN_YG_2 && Storage_yg
            return await SaveAsync(string.Empty, token);
#else
            await UniTask.CompletedTask;
            token.ThrowIfCancellationRequested();
            return false;
#endif
        }

#if PLUGIN_YG_2 && Storage_yg
        /// <summary>
        /// Дождаться инициализации SDK. Это и есть та настоящая асинхронность, ради которой
        /// интерфейс сделан асинхронным с первого дня: на старте вкладки SDK ещё не ответил,
        /// а спрашивать облако до этого бессмысленно.
        ///
        /// Ожидание ОБЯЗАНО быть с таймаутом: правило docs/04 «всё, что ждёт, ждёт с токеном»
        /// защищает от переживания партии, но не от молчащей сети. Игрок, у которого
        /// не ответил SDK, должен получить локальный сейв и играть, а не смотреть в пустой экран.
        /// </summary>
        private static async UniTask<bool> WaitForSdkAsync(CancellationToken token)
        {
            if (YG.YG2.isSDKEnabled)
                return true;

            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfterSlim(TimeSpan.FromSeconds(SDK_WAIT_SECONDS));

            try
            {
                await UniTask.WaitUntil(() => YG.YG2.isSDKEnabled, cancellationToken: timeoutCts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                // Отмена снаружи (выход из игры) — пробрасываем, это не наш случай.
                token.ThrowIfCancellationRequested();
                return false;
            }
        }
#endif
    }
}
