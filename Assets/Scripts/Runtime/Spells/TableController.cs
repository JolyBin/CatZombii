using Core.Flask;
using Core.Flask.Models;
using Core.Spells.UI;
using Core.Steps;
using System;
using System.Collections.Generic;
using Utility.Services.Localization;
using Utility.Services.UI;

namespace Core.Spells
{
    /// <summary>
    /// Котёл и ЧАСЫ КОТЛА — ядро игры (docs/10 §1), переведённое на ХОДЫ (docs/10 §0.2).
    ///
    /// ПРАВИЛО ХВОСТА (решение владельца 02.08.2026, после живой игры).
    /// У каждого элемента свой запас в N тактов, но СЧЁТЧИК ИДЁТ ТОЛЬКО У ХВОСТОВОГО —
    /// последнего положенного. Дотикал — уходит именно он, и следующий элемент
    /// становится хвостом со СВОИМИ N тактов, с полного. Остальные не тикают вовсе.
    ///
    /// Почему так, а не «у каждого свой независимый срок», как было в И1. Прежняя версия
    /// раздавала кольца по порядку гибели (ближайший срок — на хвостовом слоте), из-за чего
    /// свежий элемент получал самый старый срок, а старый выглядел перезапущенным.
    /// Владелец увидел это в игре и назвал ложью — справедливо: истекало одно кольцо,
    /// а улетал элемент из другого слота. Теперь истекает и исчезает ОДИН И ТОТ ЖЕ слот,
    /// и «у каждого своё время» перестало врать, потому что своё время идёт по очереди.
    ///
    /// Цена решения, которую надо знать при калибровке: давление стало НЕ накопительным.
    /// Раньше сроки шли параллельно и котёл старел целиком; теперь опасен только текущий
    /// хвост, и котёл живёт бесконечно, пока игрок укладывает каждое следующее схлопывание
    /// в N тактов. Значит N обязано лежать ЧУТЬ ВЫШЕ замеренного «переливов на схлопывание»,
    /// а не выводиться из секунд: при N сильно больше замера ядро становится декорацией.
    ///
    /// Единица срока — ТАКТ МИРА, то есть одно действие игрока, а не секунда. Смысл
    /// механики от этого меняется, а не только единица: в секундах остывание проверяло
    /// скорость рук, в тактах оно стало бюджетом ходов — «сколько переливов я готов
    /// потратить на этот рецепт» превращается в решение. Часы идут только когда ходит
    /// игрок, поэтому «передышка как банк» невозможна по построению: пока он не ходит,
    /// не идёт вообще ничего.
    ///
    /// Варка, как и раньше, очищает котёл целиком — и, в отличие от прежней версии,
    /// САМА СТОИТ ТАКТА (см. <see cref="CheckRepit"/>).
    ///
    /// Живёт это здесь, а не в модели колб: по поправке П1 (docs/10 §8) часы котла
    /// не блокируются отсутствием индекса и порядка у Flask.
    /// </summary>
    public class TableController : ITickable
    {
        public event Action<BaseSpell> OnSuccessfulMerge;

        /// <summary>
        /// Игрок нажал «варить». Это ВТОРОЕ действие игрока после перелива, и оно тоже
        /// двигает мир: на него подписаны часы мира (см. StepsController).
        /// </summary>
        public event Action OnBrewCommand;

        private readonly IUIService _uiService;
        private readonly FlaskController _flaskController;

        private UITableWindow _window;
        private Table _table;
        private List<Element> _currentElements;

        /// <summary>
        /// ВЕСЬ таймер котла: сколько тактов осталось ХВОСТУ. Одно число, потому что
        /// по правилу хвоста тикает ровно один элемент, а у остальных запас цел
        /// и ждёт своей очереди — хранить его копиями значило бы хранить константу N
        /// столько раз, сколько элементов в котле.
        ///
        /// Здесь же и вся разница с И1: там жила <c>Queue&lt;int&gt; _coolingDeadlines</c>
        /// с монотонными часами <c>_clockTacts</c>, и k-й срок очереди рисовался на k-м
        /// слоте с конца. Очередь выродилась в этот счётчик вместе с самим правилом.
        ///
        /// Ноль означает «никто не тикает»: котёл пуст либо остывание выключено ручкой
        /// (<see cref="UITableWindow.CoolingTacts"/> = 0).
        /// </summary>
        private int _tailRemainingTacts;

        private Action<Element> _onFlaskFullAction;

        /// <summary>
        /// Ввод заблокирован окном итога. Варка — такое же действие игрока, как перелив,
        /// и под окном победы/поражения она обязана молчать вместе с колбами:
        /// иначе заклинание прилетело бы в уже закончившийся бой.
        /// </summary>
        private bool _isInputLocked;

        /// <summary>
        /// Рецепты приходят СПИСКОМ, а не книгой: источник — экипированная колода
        /// (docs/10 §13.1), и котёл не должен знать, из чего она собрана. Книга здесь
        /// была лишним звеном и раньше — <see cref="Table"/> и так строился
        /// из <c>Combination[]</c>.
        /// </summary>
        public TableController(Combination[] combinations, FlaskController flaskController, IUIService uiService)
        {
            _uiService = uiService;
            _flaskController = flaskController;
            _table = new(combinations);
            _currentElements = new ();
            _tailRemainingTacts = 0;
            _window = _uiService.Show<UITableWindow>();
        }

        /// <summary>
        /// Поднимает котёл и обнуляет его часы. Ждать больше нечего, поэтому и токен
        /// отмены здесь не нужен: часы двигает игрок, а не таймер.
        /// </summary>
        public void Init()
        {
            _currentElements = new ();
            _tailRemainingTacts = 0;
            _isInputLocked = false;

            _onFlaskFullAction = AddElement;
            _flaskController.OnFlaskFull += _onFlaskFullAction;
            _window.OnClickCheckCombinationButton += CheckRepit;

            _window.ClearCooling();
            UpdatePreview();
        }

        /// <summary>
        /// ВАРКА. Стоит такта — решение сознательное.
        ///
        /// Бесплатная варка ломает ровно то, ради чего затевалась пошаговость: игрок
        /// доливал бы до последнего возможного хода и забирал награду даром, а «забрать
        /// сейчас или достроить» перестало бы быть выбором — у одной из веток не было
        /// бы цены. С тактом обе ветки платят: доливаешь — платишь переливом, варишь —
        /// платишь ходом соседей. Заодно перестаёт быть бесплатным одноэлементный
        /// кэш-аут, то есть страховка остаётся страховкой, а не доминирующей стратегией.
        ///
        /// Такт берётся ПОСЛЕДНИМ, уже после применения зелья и очистки котла: цена
        /// платится после награды, иначе последний ход (сварить лечение на трёх HP или
        /// успеть до остывания) был бы проигран по построению. Разбор — в WorldClock.Tick.
        ///
        /// Провал варки тоже стоит такта: котёл он очищает так же, как успех, и «нажму,
        /// вдруг сработает» обязано иметь цену.
        /// </summary>
        public void CheckRepit()
        {
            if (_isInputLocked)
                return;

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
                _window.ShowResult(false, Localization.Get(LocKeys.TableBrewFailed));
            }

            // варка очищает котёл целиком — вместе со всеми сроками. Это и есть цена решения
            _currentElements = new();
            _tailRemainingTacts = 0;
            _window.ClearFlasks();
            _window.ClearCooling();
            UpdatePreview();

            OnBrewCommand?.Invoke();
        }

        /// <summary>Партия кончилась: кнопка варки больше не действие, а картинка.</summary>
        public void LockInput() => _isInputLocked = true;

        public void UnlockInput() => _isInputLocked = false;

        public void Exit()
        {
            if (_onFlaskFullAction != null)
            {
                _flaskController.OnFlaskFull -= _onFlaskFullAction;
                _onFlaskFullAction = null;
            }
            _window.OnClickCheckCombinationButton -= CheckRepit;

            _currentElements = new();
            _tailRemainingTacts = 0;

            OnSuccessfulMerge = null;
            OnBrewCommand = null;
            _window.ClearCooling();
            _window.Hide();
        }

        /// <summary>
        /// Один такт мира: первая фаза (обоснование порядка — в WorldClock.Tick).
        ///
        /// Тикает РОВНО ОДИН счётчик — хвостовой. За такт из котла уходит не больше
        /// одного элемента: новый хвост получает полный запас, а он не меньше единицы,
        /// значит цепной обвал невозможен по построению. Прежний <c>while</c> по очереди
        /// сроков вместе с очередью и уехал.
        /// </summary>
        public void Tick()
        {
            // Ручку остывания крутят вживую — это её заявленное свойство, и правка
            // обязана действовать с ЭТОГО такта:
            //   0 — остывание выключено, замираем немедленно, а не «дотикав» текущий хвост
            //       (иначе чистый замер темпа стоил бы игроку одного элемента);
            //   был 0, стал N — хвост получает запас, не дожидаясь следующей вставки.
            // Уменьшение N с непустым хвостом намеренно не досрезается: текущий элемент
            // доживает по прежней ручке, следующий уже по новой — на один элемент неточно
            // и не стоит развилки в модели.
            if (_window.CoolingTacts <= 0 || _currentElements.Count == 0)
            {
                _tailRemainingTacts = 0;
                PushCoolingToView();
                return;
            }
            if (_tailRemainingTacts <= 0)
                RestartTail(tactIsStillComing: false);

            _tailRemainingTacts--;
            if (_tailRemainingTacts <= 0)
            {
                // истёк и ушёл — ОДИН И ТОТ ЖЕ элемент; следом хвостом становится
                // сосед и начинает свои N с полного
                DropTail();
                RestartTail(tactIsStillComing: false);
            }

            PushCoolingToView();
        }

        private void AddElement(Element element)
        {
            // котёл переполнен — лишний элемент не берём, иначе модель разъедется с UI
            if (_window.IsFull)
                return;

            _currentElements.Add(element);
            _window.ShowFullFlask(element.Texture);

            // свежий элемент — новый хвост: прежний замирает на своём нетронутом запасе
            RestartTail(tactIsStillComing: true);

            PushCoolingToView();
            UpdatePreview();
        }

        /// <summary>
        /// Выдаёт хвосту ПОЛНЫЙ запас. Здесь и только здесь живёт правило «с полного,
        /// а не с остатка»: элемент, переставший быть хвостом, свой остаток не хранит —
        /// вернувшись в хвост, он начинает заново.
        ///
        /// Альтернативой было заморозить остаток и возобновить с него. Она жёстче
        /// (котёл стареет необратимо) и владельцем сознательно не выбрана; переключение
        /// стоило бы одного массива остатков параллельно <see cref="_currentElements"/>.
        /// </summary>
        /// <param name="tactIsStillComing">
        /// true — запас выдан ВНУТРИ перелива, такт которого ещё не пришёл: элемент падает
        /// в котёл из <c>Flask.PushElement → OnFlaskFull</c>, а <c>WorldClock.Tick</c>
        /// прилетает следующей строкой <c>FlaskController.MoveBall</c>. Без поправки свежий
        /// элемент терял бы ход за перелив, который его же и создал, и его кольцо рождалось
        /// бы неполным — картинка врала бы про срок с первого кадра.
        /// false — запас выдан внутри уже засчитанного такта (<see cref="Tick"/>):
        /// первым списанием будет следующий такт.
        /// </param>
        private void RestartTail(bool tactIsStillComing)
        {
            int budget = _window.CoolingTacts;
            // пустой котёл или выключенное ручкой остывание — тикать нечему
            if (_currentElements.Count == 0 || budget <= 0)
            {
                _tailRemainingTacts = 0;
                return;
            }

            _tailRemainingTacts = budget + (tactIsStillComing ? 1 : 0);
        }

        private void DropTail()
        {
            if (_currentElements.Count == 0)
                return;

            _currentElements.RemoveAt(_currentElements.Count - 1);
            _window.RemoveTailFlask();
            UpdatePreview();
        }

        /// <summary>
        /// Виду отдаётся ОДНО число — остаток хвоста. Кто хвост, вид знает сам: это
        /// последний занятый слот, и он же единственный, чьё кольцо живое. Раскладка
        /// «k-й срок на k-м слоте с конца» больше не нужна вместе с самой очередью сроков.
        /// </summary>
        private void PushCoolingToView() => _window.SetCooling(_tailRemainingTacts, _window.CoolingTacts);

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
