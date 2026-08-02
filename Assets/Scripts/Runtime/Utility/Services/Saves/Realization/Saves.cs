using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Utility.Services.Saves
{
    /// <summary>
    /// Статический фасад над <see cref="ISaveService"/> — по образцу
    /// <c>Utility.Services.Localization.Localization</c>, и по той же причине.
    ///
    /// Почему статика, если в проекте «логика — POCO, связь через конструкторы».
    /// Прогресс нужен ДВУМ разным мирам: контроллерам (им конструктор передать можно)
    /// и компонентам на сцене вроде <c>GameManager</c>, которым конструктор не передать
    /// в принципе — их создаёт Unity. Прецедентов в проекте уже два (<c>UIService.Instance</c>
    /// и <c>Localization</c>), и заводить третий способ доступа к сервису значило бы
    /// сделать проект менее предсказуемым, а не более.
    ///
    /// За фасадом обычный POCO, который можно подменить (<see cref="Install"/>) —
    /// в тесте, в редакторной проверке и в отладочном меню.
    /// </summary>
    public static class Saves
    {
        private static ISaveService _service;

        public static ISaveService Service
        {
            get
            {
                if (_service == null)
                    Install(CreateDefault());
                return _service;
            }
        }

        /// <summary>Прогресс. Никогда не <c>null</c>: до загрузки — новый профиль.</summary>
        public static PlayerProfile Profile => Service.Profile;

        public static bool IsLoaded => Service.IsLoaded;

        /// <summary>Профиль прочитан с носителя. Переживает подмену сервиса.</summary>
        public static event Action OnProfileLoaded;

        public static UniTask LoadAsync(CancellationToken token) => Service.LoadAsync(token);

        public static UniTask<bool> SaveAsync(CancellationToken token) => Service.SaveAsync(token);

        public static void RequestSave(string reason) => Service.RequestSave(reason);

        public static void FlushIfDirty() => Service.FlushIfDirty();

        /// <summary>
        /// Подменить реализацию. Нужна редакторным проверкам и отладке; игровой код её не зовёт.
        /// </summary>
        public static void Install(ISaveService service)
        {
            if (_service != null)
                _service.OnProfileLoaded -= RaiseProfileLoaded;

            if (_service is SaveService disposable)
                disposable.Dispose();

            _service = service;

            if (_service != null)
                _service.OnProfileLoaded += RaiseProfileLoaded;
        }

        /// <summary>
        /// Сборка сервиса по умолчанию. ЗДЕСЬ И ТОЛЬКО ЗДЕСЬ задан порядок носителей:
        /// облако площадки → локальные префы. Порядок — правило продукта («аккаунт важнее
        /// устройства»), поэтому он записан одной видимой строкой, а не размазан по классам.
        ///
        /// Сегодня облако выключено дефайном и молчит, так что фактический носитель один.
        /// Что нужно, чтобы включить облако, написано в <see cref="PluginYGSaveStorage"/>.
        /// </summary>
        private static ISaveService CreateDefault()
        {
            List<ISaveStorage> storages = new()
            {
                new PluginYGSaveStorage(),
                new PlayerPrefsSaveStorage(),
            };
            return new SaveService(storages);
        }

        private static void RaiseProfileLoaded() => OnProfileLoaded?.Invoke();

#if UNITY_EDITOR
        /// <summary>
        /// Статика переживает выход из Play Mode при выключенном Domain Reload, а вместе
        /// с ней — профиль и подписки прошлого запуска. Для системы сохранений это не просто
        /// утечка: непочищенный профиль сделал бы проверку «вышел и зашёл» бессмысленной —
        /// прогресс «сохранялся» бы и без единой записи на диск.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsForEditor()
        {
            Install(null);
            OnProfileLoaded = null;
        }
#endif
    }
}
