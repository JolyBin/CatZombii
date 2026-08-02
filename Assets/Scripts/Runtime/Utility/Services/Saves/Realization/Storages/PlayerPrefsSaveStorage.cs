using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Utility.Services.Saves
{
    /// <summary>
    /// НОСИТЕЛЬ ПО УМОЛЧАНИЮ. Работает везде, где вообще работает Unity: в редакторе,
    /// в WebGL, на мобилках, в Standalone. Именно он и есть «фолбэк, когда площадки нет» —
    /// отдельной пустышки заводить не за чем, площадка добавляет носитель, а не заменяет его.
    ///
    /// ЧТО ЭТО ФИЗИЧЕСКИ. В WebGL <c>PlayerPrefs</c> — не файл, а IndexedDB через
    /// эмуляцию файловой системы Emscripten. <c>PlayerPrefs.Save()</c> просит браузер
    /// сбросить кэш на диск, но БРАУЗЕР ДЕЛАЕТ ЭТО КОГДА СОЧТЁТ НУЖНЫМ — синхронной
    /// гарантии «записалось» здесь не существует ни у кого. Это первая из двух причин,
    /// по которым <see cref="ISaveStorage"/> асинхронный (вторая — облако).
    ///
    /// ЧЕГО ЭТОТ НОСИТЕЛЬ НЕ УМЕЕТ и почему это нормально:
    ///  — он не переезжает между устройствами и между браузерами;
    ///  — его сносит очистка данных сайта.
    /// Оба пункта закрывает облако площадки, когда его подключат. До тех пор потеря
    /// прогресса от «почистил кэш» — известная и принятая цена, а не сюрприз.
    /// </summary>
    public class PlayerPrefsSaveStorage : ISaveStorage
    {
        /// <summary>
        /// Ключ. С префиксом игры, потому что <c>PlayerPrefs</c> — общее пространство имён
        /// на весь домен площадки: сосед по <c>yandex.ru/games</c> с ключом <c>profile</c>
        /// не должен нас видеть, а мы его.
        /// </summary>
        public const string PREFS_KEY = "catzombii.profile";

        public string Name => "PlayerPrefs (локально)";

        public bool IsAvailable => true;

        public UniTask<string> LoadAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                return UniTask.FromResult(PlayerPrefs.GetString(PREFS_KEY, string.Empty));
            }
            catch (Exception exception)
            {
                // Живой сценарий: приватный режим браузера, где хранилище сайту запрещено.
                // Игра обязана в этом случае просто идти дальше без сейва.
                Debug.LogError($"[Saves] {Name}: чтение не удалось ({exception.Message}). Играем без сохранения.");
                return UniTask.FromResult(string.Empty);
            }
        }

        public UniTask<bool> SaveAsync(string json, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                PlayerPrefs.SetString(PREFS_KEY, json);
                // Save() обязателен: без него запись живёт только до конца сессии,
                // а «закрыл вкладку» в вебе — это и есть конец сессии.
                PlayerPrefs.Save();
                return UniTask.FromResult(true);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Saves] {Name}: запись не удалась ({exception.Message}).");
                return UniTask.FromResult(false);
            }
        }

        public UniTask<bool> DeleteAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                PlayerPrefs.DeleteKey(PREFS_KEY);
                PlayerPrefs.Save();
                return UniTask.FromResult(true);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Saves] {Name}: удаление не удалось ({exception.Message}).");
                return UniTask.FromResult(false);
            }
        }
    }
}
