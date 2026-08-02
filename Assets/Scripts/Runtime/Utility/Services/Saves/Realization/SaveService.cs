using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Utility.Services.Saves
{
    /// <summary>
    /// Реализация <see cref="ISaveService"/> — обычный C#-класс (POCO), как и вся логика
    /// проекта: <see cref="MonoBehaviour"/> здесь не нужен. Точка доступа для кода,
    /// которому конструктор не передать, — статический фасад <see cref="Saves"/>.
    ///
    /// Что этот класс делает и чего НЕ делает. Он владеет профилем и порядком носителей;
    /// он НЕ знает, как профиль превращается в строку (<see cref="ProfileSerializer"/>)
    /// и куда эта строка ложится (<see cref="ISaveStorage"/>). Ровно эта развилка
    /// и есть решение владельца: «система сейва единая, а реализация сохранения
    /// на платформе уже конкретная».
    /// </summary>
    public class SaveService : ISaveService
    {
        public PlayerProfile Profile { get; private set; }

        public bool IsLoaded { get; private set; }

        public bool IsDirty { get; private set; }

        public event Action OnProfileLoaded;

        private readonly IReadOnlyList<ISaveStorage> _storages;

        /// <summary>
        /// Время жизни самого сервиса. Нужен <see cref="RequestSave"/>: у обработчика
        /// кнопки своего токена нет, а запись без токена пережила бы выход из Play Mode
        /// и продолжила писать в уже мёртвый редактор.
        /// </summary>
        private readonly CancellationTokenSource _lifetime = new();

        /// <summary>Запись уже идёт. Две параллельные записи в одно хранилище — гонка за содержимое.</summary>
        private bool _saveInFlight;

        /// <summary>Пока писали, профиль успел измениться — надо будет записать ещё раз.</summary>
        private bool _saveQueued;

        public SaveService(IReadOnlyList<ISaveStorage> storages)
        {
            _storages = storages ?? Array.Empty<ISaveStorage>();
            // Профиль есть ВСЕГДА и с первой строки: см. комментарий к ISaveService.Profile.
            Profile = PlayerProfile.CreateNew();
        }

        /// <summary>
        /// Опрос носителей ПО ПОРЯДКУ, первый ответивший выигрывает — ровно как у языка
        /// (<c>LocalizationService.ResolveLanguage</c>). Порядок задаёт тот, кто собирает
        /// сервис (<see cref="Saves"/>), а не этот класс.
        ///
        /// ЧЕГО ЗДЕСЬ СОЗНАТЕЛЬНО НЕТ: слияния двух прогрессов. Если однажды и в облаке,
        /// и локально окажется по сейву, победит первый в списке, а не «лучший». Выбрать
        /// «лучший» — продуктовое решение (больший уровень? больший кошелёк? свежий?),
        /// а не техническое, и принимать его в этом методе украдкой было бы хуже,
        /// чем не принимать вовсе. Пока облако выключено, сливать нечего.
        /// </summary>
        public async UniTask LoadAsync(CancellationToken token)
        {
            foreach (ISaveStorage storage in _storages)
            {
                if (storage == null || !storage.IsAvailable)
                    continue;

                string json;
                try
                {
                    json = await storage.LoadAsync(token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // Носитель обязан гасить свои сбои сам, но если не погасил —
                    // это не повод остаться без прогресса: идём к следующему.
                    Debug.LogError($"[Saves] Носитель «{storage.Name}» упал при чтении: {exception}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(json))
                    continue;

                if (!ProfileSerializer.TryFromJson(json, out PlayerProfile loaded))
                {
                    // Битый сейв уже отругался в ProfileSerializer. Пробуем следующий
                    // носитель: испорченный локальный кэш не должен отменять облако.
                    continue;
                }

                Profile = loaded;
                IsDirty = false;
                IsLoaded = true;
                Debug.Log($"[Saves] Прогресс загружен из «{storage.Name}»: {Profile}");
                OnProfileLoaded?.Invoke();
                return;
            }

            // Сюда приходят два разных случая, и различать их в логе важно:
            // «первый запуск» — норма, «всё сломалось» — нет.
            Profile = PlayerProfile.CreateNew();
            IsDirty = false;
            IsLoaded = true;
            Debug.Log($"[Saves] Сохранения нет — новый профиль: {Profile}");
            OnProfileLoaded?.Invoke();
        }

        public async UniTask<bool> SaveAsync(CancellationToken token)
        {
            if (_saveInFlight)
            {
                // Профиль — общий объект, а не копия: идущая запись подхватит и эти
                // изменения на следующем витке. Поэтому «принято» здесь честное.
                _saveQueued = true;
                return true;
            }

            _saveInFlight = true;
            try
            {
                bool written;
                do
                {
                    _saveQueued = false;
                    written = await WriteAsync(token);
                }
                while (_saveQueued && !token.IsCancellationRequested);

                return written;
            }
            finally
            {
                _saveInFlight = false;
            }
        }

        public void RequestSave(string reason)
        {
            IsDirty = true;
            Debug.Log($"[Saves] Сохранение: {reason}. {Profile}");
            FireAndForget();
        }

        public void FlushIfDirty()
        {
            if (!IsDirty)
                return;
            Debug.Log($"[Saves] Досохранение перед выходом. {Profile}");
            FireAndForget();
        }

        /// <summary>
        /// Закрыть сервис: гасит незавершённую запись. Зовётся при выгрузке
        /// (домен-релоад редактора, подмена сервиса).
        /// </summary>
        public void Dispose()
        {
            if (!_lifetime.IsCancellationRequested)
                _lifetime.Cancel();
            _lifetime.Dispose();
            OnProfileLoaded = null;
        }

        /// <summary>
        /// Запуск записи без ожидания — для синхронных вызывающих. Единственное место
        /// в сервисе, где задача уходит в <c>Forget()</c>, и поэтому единственное, где
        /// исключение обязано быть поймано руками: незамеченное исключение в fire-and-forget
        /// это молча несохранённый прогресс.
        /// </summary>
        private void FireAndForget() => SaveSafeAsync().Forget();

        private async UniTaskVoid SaveSafeAsync()
        {
            try
            {
                await SaveAsync(_lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // штатно: игра закрылась раньше, чем записалось
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>Один проход записи по всем доступным носителям.</summary>
        private async UniTask<bool> WriteAsync(CancellationToken token)
        {
            string json = ProfileSerializer.ToJson(Profile);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[Saves] Профиль не сериализовался — записывать нечего.");
                return false;
            }

            bool anyWritten = false;
            bool anyAvailable = false;

            foreach (ISaveStorage storage in _storages)
            {
                if (storage == null || !storage.IsAvailable)
                    continue;
                anyAvailable = true;

                try
                {
                    // Пишем во ВСЕ носители, а не в первый: локальный сейв обязан остаться
                    // рабочим и без сети, а облачный — подхватиться на другом устройстве.
                    if (await storage.SaveAsync(json, token))
                        anyWritten = true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Saves] Носитель «{storage.Name}» упал при записи: {exception}");
                }
            }

            if (!anyAvailable)
            {
                Debug.LogWarning("[Saves] Ни одного носителя — прогресс живёт только до конца сессии.");
                return false;
            }

            if (anyWritten)
                IsDirty = false;
            else
                Debug.LogError("[Saves] ПРОГРЕСС НЕ ЗАПИСАН НИ В ОДИН НОСИТЕЛЬ.");

            return anyWritten;
        }
    }
}
