using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Utility.Services.Saves
{
    /// <summary>
    /// Единственный вход в сохранённый прогресс. Игровой код держит здесь ВСЁ, что обязано
    /// пережить перезагрузку вкладки, и не знает ни про PlayerPrefs, ни про облако.
    ///
    /// Блокер №1 роадмапа (docs/07, строки 15-21): прогресс живёт в
    /// <c>HomeController._configIndex</c> в оперативной памяти, перезагрузил вкладку —
    /// начал с первого уровня. В вебе это не неудобство, а отсутствие D1 как метрики.
    /// </summary>
    public interface ISaveService
    {
        /// <summary>
        /// Профиль. НИКОГДА не <c>null</c> — до загрузки это новый профиль по умолчанию.
        /// Так сделано специально: обращение к прогрессу не имеет права требовать
        /// проверки на null в каждой точке вызова, иначе одна забытая проверка = NRE в релизе.
        /// </summary>
        PlayerProfile Profile { get; }

        /// <summary>Загрузка уже отработала (успешно или с откатом к новому профилю).</summary>
        bool IsLoaded { get; }

        /// <summary>Профиль менялся после последней успешной записи.</summary>
        bool IsDirty { get; }

        /// <summary>Профиль прочитан с носителя и подставлен. Кто показывает прогресс — перечитывает.</summary>
        event Action OnProfileLoaded;

        /// <summary>
        /// Прочитать профиль. Зовётся ОДИН РАЗ на старте, до создания
        /// <c>HomeController</c>: контроллеры читают прогресс в конструкторе.
        /// </summary>
        UniTask LoadAsync(CancellationToken token);

        /// <summary>Записать профиль во все доступные носители. <c>true</c> — записалось хотя бы в один.</summary>
        UniTask<bool> SaveAsync(CancellationToken token);

        /// <summary>
        /// «Сохранись, я не могу ждать» — для обработчиков кнопок и прочего кода,
        /// который синхронный по своей природе. Причина уходит в лог: точек сохранения
        /// намеренно мало, и каждая обязана быть узнаваемой в логе одной строкой.
        /// </summary>
        void RequestSave(string reason);

        /// <summary>Записать, если есть что. Страховка на сворачивание вкладки и выход.</summary>
        void FlushIfDirty();
    }
}
