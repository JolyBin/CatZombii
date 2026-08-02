using System.Threading;
using Core.Battle;
using Core.Spells;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Utility.Services.Saves;

namespace Meta
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private UIService _uiService;
        [SerializeField] private Book _startBook;
        [SerializeField] private BattleConfig[] _currentLevels;

        private HomeController _homeController;

        /// <summary>
        /// Старт стал асинхронным ради ОДНОЙ вещи — прогресс обязан быть прочитан
        /// ДО создания <see cref="HomeController"/>: и уровень, и герой читаются
        /// в конструкторах контроллеров.
        ///
        /// Почему нельзя «показать главный экран, а прогресс подставить потом»:
        /// игрок увидел бы уровень 1, а через мгновение — уровень 5. В вебе, где
        /// вкладку перезагружают постоянно, это выглядит как потерянный прогресс,
        /// то есть ровно как тот дефект, который сейвы и чинят. Локальный носитель
        /// отвечает в том же кадре, облако — нет, и второй случай надо было заложить
        /// сразу, а не переделывать под него запуск потом.
        ///
        /// Токен — по правилу docs/04 «всё, что ждёт, ждёт с токеном»:
        /// <c>GetCancellationTokenOnDestroy</c> гасит ожидание облака, если сцену
        /// выгрузили раньше ответа (в редакторе — выход из Play Mode).
        /// </summary>
        private void Start() => Boot(this.GetCancellationTokenOnDestroy()).Forget();

        private async UniTaskVoid Boot(CancellationToken token)
        {
            _uiService.HideAll();

            await Saves.LoadAsync(token);

            _homeController = new HomeController(_uiService, _startBook, _currentLevels);
        }

        /// <summary>
        /// Страховка, а не точка сохранения. Прогресс пишется там, где он меняется
        /// (см. <c>HomeController.RegisterNodeCleared</c> и <c>HeroController</c>), поэтому
        /// здесь почти всегда нечего делать — <c>FlushIfDirty</c> это и проверяет.
        ///
        /// Нужна ради веба: свёрнутую вкладку браузер вправе выгрузить без предупреждения,
        /// а <c>OnApplicationQuit</c> там не гарантирован вовсе.
        /// </summary>
        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
                Saves.FlushIfDirty();
        }

        private void OnApplicationQuit() => Saves.FlushIfDirty();

        /// <summary>
        /// Единственная точка разбора: сцена одна, значит её выгрузка (в редакторе —
        /// выход из Play Mode) и есть конец сессии. Без этого события меты и карты
        /// пережили бы Play Mode ссылками на уничтоженные окна — тот самый класс
        /// утечек, ради которого в проекте заведён <c>IAction.ClearAction</c>.
        /// </summary>
        private void OnDestroy() => _homeController?.Exit();
    }
}
