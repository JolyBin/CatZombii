using Core.Flask;
using Core.Flask.Models;
using Core.Spells.UI;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility.Services.UI;

namespace Core.Spells
{
    /// <summary>
    /// Котёл и ЧАСЫ КОТЛА — ядро игры (docs/10 §1).
    ///
    /// Каждый попавший в котёл элемент начинает стынуть независимо. Истёк его срок —
    /// из котла выпадает ХВОСТ, последний положенный элемент, а не тот, чей таймер истёк:
    /// наказание пропорционально жадности, а голова длинной цепочки живёт дольше.
    ///
    /// Часы идут всегда, в том числе в передышке между волнами, — иначе передышка
    /// превращается в банк. Варка, как и раньше, очищает котёл целиком.
    ///
    /// Живёт это здесь, а не в модели колб: по поправке П1 (docs/10 §8) часы котла
    /// не блокируются отсутствием индекса и порядка у Flask.
    /// </summary>
    public class TableController
    {
        public event Action<BaseSpell> OnSuccessfulMerge;

        /// <summary>Шаг модели часов — как у боевых таймеров (TargetController.TIMER_STEP).</summary>
        private const int TIMER_STEP = 100;

        private readonly IUIService _uiService;
        private readonly FlaskController _flaskController;

        private UITableWindow _window;
        private Table _table;
        private List<Element> _currentElements;

        /// <summary>
        /// Сроки остывания: ровно один на каждый элемент в котле, в порядке вставки.
        /// Голова очереди — ближайшая потеря, именно её и показывает кольцо на ободе:
        /// раз каждая вставка запускает свой срок, а срабатывание любого снимает хвост,
        /// время до следующей потери — одно число, а не четыре (docs/12 §5.2).
        /// </summary>
        private Queue<float> _coolingDeadlines;

        /// <summary>
        /// Часы котла в собственных секундах, накопленных шагами таймера, а не Time.time.
        /// Так Time.timeScale = 0 (пауза под рекламу, поправка П4) морозит котёл ровно
        /// так же, как боевые таймеры, и кольцо не расходится с моделью.
        /// </summary>
        private float _clockTime;

        private Action<Element> _onFlaskFullAction;
        private bool _isLive;

        public TableController(Book currentBook, FlaskController flaskController, IUIService uiService)
        {
            _uiService = uiService;
            _flaskController = flaskController;
            _table = new(currentBook.Combinations);
            _currentElements = new ();
            _coolingDeadlines = new ();
            _window = _uiService.Show<UITableWindow>();
        }

        /// <summary>
        /// Запускает котёл и его часы. Токен — боевой: всё, что ждёт, ждёт с токеном,
        /// часы обязаны умереть вместе с боем.
        /// </summary>
        public void Init(CancellationToken token)
        {
            _currentElements = new ();
            _coolingDeadlines = new ();
            _clockTime = 0f;

            _onFlaskFullAction = AddElement;
            _flaskController.OnFlaskFull += _onFlaskFullAction;
            _window.OnClickCheckCombinationButton += CheckRepit;

            _window.ClearCooling();
            UpdatePreview();

            _isLive = true;
            CoolingClock(token).Forget();
        }

        public void CheckRepit()
        {
            BaseSpellConfig spell;
            if (_table.TryGetSpell(_currentElements.ToArray(), out spell))
            {
                _window.ShowResult(true, spell.Name);
                OnSuccessfulMerge?.Invoke(spell.GetSpell());
            }
            else
            {
                // Провал штатен и част, ветка живая. Закон префикса (docs/10 §6) обещает,
                // что префикс РЕЦЕПТА — тоже рецепт, и ничего не обещает про произвольную
                // цепочку, которую набрал игрок: он не выбирает, что схлопнется, только
                // когда проверить. «Fire > Fighting» рецептом не является, а «Fighting >
                // Fighting» — внутренний узел дерева по дороге к ультимейту без заклинания.
                _window.ShowResult(false, "Не сварилось");
            }

            // варка очищает котёл целиком — вместе с часами. Это и есть цена решения
            _currentElements = new();
            _coolingDeadlines.Clear();
            _window.ClearFlasks();
            _window.ClearCooling();
            UpdatePreview();
        }

        public void Exit()
        {
            _isLive = false;

            if (_onFlaskFullAction != null)
            {
                _flaskController.OnFlaskFull -= _onFlaskFullAction;
                _onFlaskFullAction = null;
            }
            _window.OnClickCheckCombinationButton -= CheckRepit;

            _currentElements = new();
            _coolingDeadlines.Clear();

            OnSuccessfulMerge = null;
            _window.ClearCooling();
            _window.Hide();
        }

        private void AddElement(Element element)
        {
            // котёл переполнен — лишний элемент не берём, иначе модель разъедется с UI
            if (_window.IsFull)
                return;

            _currentElements.Add(element);
            _coolingDeadlines.Enqueue(_clockTime + _window.CoolingSeconds);
            _window.ShowFullFlask(element.Texture);

            PushCoolingToView();
            UpdatePreview();
        }

        private async UniTaskVoid CoolingClock(CancellationToken token)
        {
            try
            {
                while (_isLive && !token.IsCancellationRequested)
                {
                    bool isCanceled = await UniTask.Delay(TIMER_STEP, cancellationToken: token).SuppressCancellationThrow();
                    if (isCanceled)
                        return;

                    _clockTime += TIMER_STEP / 1000f;

                    while (_coolingDeadlines.Count > 0 && _coolingDeadlines.Peek() <= _clockTime)
                    {
                        _coolingDeadlines.Dequeue();
                        DropTail();
                    }

                    PushCoolingToView();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void DropTail()
        {
            if (_currentElements.Count == 0)
                return;

            _currentElements.RemoveAt(_currentElements.Count - 1);
            _window.RemoveTailFlask();
            UpdatePreview();
        }

        private void PushCoolingToView()
        {
            if (_coolingDeadlines.Count == 0)
            {
                _window.ClearCooling();
                return;
            }
            _window.SetCooling(_coolingDeadlines.Peek() - _clockTime, _window.CoolingSeconds);
        }

        private void UpdatePreview()
        {
            BaseSpellConfig spell;
            // ТОЛЬКО текущий выигрыш. Никаких «а если доложишь ещё» — напечатанный игроку
            // флоучарт назван в docs/10 §0 прямой причиной смерти press-your-luck.
            bool hasSpell = _table.TryGetSpell(_currentElements.ToArray(), out spell);
            _window.SetPreview(hasSpell ? spell.Icon : null, hasSpell ? spell.PreviewValue : 0, hasSpell);
        }
    }
}
