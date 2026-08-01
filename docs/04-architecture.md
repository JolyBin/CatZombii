# 04 — Архитектура

*Срез: 1 августа 2026, сверен с кодом после `a453a080`, `d162c4db`, `fa1b425a`.*

## Главный принцип

**MonoBehaviour только для View и единственной точки входа. Вся логика — обычные
C#-классы (POCO), связанные конструкторами и событиями.**

Это выдержано последовательно почти везде и является главной причиной, по которой
проект расширяем. Единственное исключение — `UIService`, который является
MonoBehaviour-синглтоном (см. ниже).

## Дерево владения

```
GameManager  (MonoBehaviour на объекте Main Camera, единственная точка входа)
│  _uiService, _startBook, _currentLevels[]
│
└─ HomeController                          главный экран, хранит _configIndex
   │
   ├─ HeroController                       выбор героя
   │   └─ EquipmentController              просмотр книги
   │
   └─ StepsController                      ОДНА ПАРТИЯ (создаётся на каждый бой)
      │
      ├─ FlaskController ──────► OnFlaskFull(Element)
      │                                │
      ├─ TableController ◄─────────────┘
      │   │  накапливает элементы, ищет в Table (trie)
      │   └──────────────► OnSuccessfulMerge(BaseSpell)
      │                                │
      └─ BattleController ◄────────────┘
              spell.ApplySpell(battleController, BattleToken)
          │
          ├─ OnAllEnemyDie ──► StepsController.ShowWinWindow
          └─ OnHeroDie ─────► StepsController.ShowLoseWindow
```

Обрати внимание: **связь односторонняя вниз по владению и событийная вверх**.
`FlaskController` ничего не знает про бой. `BattleController` ничего не знает
про колбы. Склейка живёт только в `StepsController`.

## Слои

| Слой | Где | Что там |
|---|---|---|
| Точка входа | `Runtime/GameManager.cs` | Единственный `MonoBehaviour` с игровой логикой |
| Мета | `Runtime/Meta/` | Главный экран, выбор героя, экипировка |
| Оркестрация | `Runtime/Steps/` | `StepsController` — склейка партии |
| Пазл | `Runtime/Flask/` | Колбы, генератор, элементы |
| Заклинания | `Runtime/Spells/` | Книга, trie, комбинации, конфиги заклинаний |
| Бой | `Runtime/Battle/` | Юниты, здоровье, таймеры атак, волны |
| Инфраструктура | `Runtime/Utility/` | `UIService`, `Pool<T>`, `IAction` |

Namespace'ы: `Core.Battle`, `Core.Flask`, `Core.Spells`, `Core.Steps`, `Meta`,
`Utility.Services.UI`, `Utility.Collections`.
У `UIService` namespace'а нет (глобальный) — так исторически.

Своих `asmdef` у игрового кода нет: всё компилируется в `Assembly-CSharp`.
Отдельные `asmdef` есть только у вендоренного UniTask.

---

## Ключевые решения и их цена

### 1. Trie для поиска комбинаций

[`Table.cs`](../Assets/Scripts/Runtime/Spells/Table.cs) +
[`Chain.cs`](../Assets/Scripts/Runtime/Spells/Models/Chain.cs)

Комбинации книги разворачиваются в префиксное дерево по `Element.ID`.
Поиск — за длину последовательности, независимо от количества комбинаций.

**Почему это правильно:** порядок элементов значим, префиксы естественно
переиспользуются (`Fire` и `Fire > Fire` — разные заклинания, живут в одной ветке),
добавление комбинации не требует перестройки.

**Цена:** дерево строится на каждый `new Table(...)`, то есть на каждый бой.
При текущих объёмах (3–5 комбинаций) это ничто.

### 2. Заклинание = конфиг-ассет + рантайм-объект

```csharp
BaseSpellConfig (ScriptableObject)  →  GetSpell()  →  BaseSpell (POCO)
                                                         │
                                ApplySpell(BattleController, CancellationToken) : UniTask
```

Конфиг хранит данные и живёт в проекте. `BaseSpell` создаётся на применение
и сам знает, что делать с боем. Новое заклинание = один файл + один ассет,
ничего в существующем коде трогать не надо.

`ApplySpell` возвращает `UniTask` и принимает токен жизни боя (`d162c4db`;
раньше был `async void` без токена). Точка запуска одна —
`BattleController.ApplySpell`, обёртка над подпиской на `OnSuccessfulMerge`:
она ждёт задачу, гасит `OperationCanceledException` тихо, а всё остальное
отправляет в `Debug.LogException`. То есть **ошибка внутри заклинания теперь
всплывает** — она видна в консоли и не теряется в WebGL, где при
`Exception Support = Explicitly Thrown Only` исключения из `async void`
глотались молча.

**Цена:** запуск всё ещё fire-and-forget — вызывающий код не ждёт заклинание
и не может по нему что-то решить. Дисциплина переехала внутрь: каждое ожидание
обязано получить токен, иначе эффект переживёт бой. Рецепт —
в [05 — Как расширять](05-extending.md).

### 3. Юнит = конфиг + рантайм + UI-виджет

[`UnitRuntime.cs`](../Assets/Scripts/Runtime/Battle/UnitRuntime.cs) склеивает три вещи:
`Health` (модель), `TargetController` (таймер и выбор цели), `UIUnit` (виджет).

Атака юнита — тоже конфиг-фабрика: `BaseUnitSpellConfig.GetUnitSpell()` →
`BaseUnitSpell.InitSpell(owner, battleController)`, который подписывается
на `owner.TargetController.OnAttack`. Именно так реализованы и обычная атака,
и трёхфазный босс.

### 4. Окна не создаются, а переключаются

[`UIWindow`](../Assets/Scripts/Runtime/Utility/Services/UIService/Realization/UIWindow.cs)
гасит `Canvas.enabled`, а не `SetActive(false)`.

**Почему:** для WebGL это заметно дешевле — не пересобирается иерархия,
не дёргаются `OnEnable`/`OnDisable`, не теряются ссылки.

**Цена:** объекты остаются активными и продолжают получать `Update`.
И — важно — [`UIService`](../Assets/Scripts/Runtime/Utility/Services/UIService/Realization/UIService.cs)
ищет окна через `FindObjectsByType<UIWindow>(FindObjectsInactive.Exclude)`,
то есть **все окна обязаны быть активны на сцене в момент `Awake`**.
`GameManager.Start()` сразу вызывает `HideAll()`.

### 5. Ручное управление подписками через `IAction`

```csharp
public interface IAction { void ClearAction(); }
```

Контроллеры собирают подписчиков в `List<IAction>` и чистят в `Exit()`.
Контроллеры также занулают собственные события (`OnFlaskFull = null` и т.п.).

**Почему так:** проще, и при перезапуске партии всё пересоздаётся заново.

**Цена:** дисциплина держится на человеке. Забыл добавить `ClearAction()` —
получил утечку подписки между боями. Это самое хрупкое место архитектуры.
При добавлении новой подписки **обязательно** ищи соответствующий `Exit()`.
Отдельно опасны подписки лямбдой: снять такую нельзя в принципе, поэтому
обработчики, которые надо отписывать, хранятся в поле или объявляются методом
(как `_applySpellAction` и `HeroDie` в `BattleController`).

### 5а. Отмена: токен партии → связанный токен боя

Подписки `IAction` отвечают за «кто на что подписан», но не за «что уже
запущено и ещё крутится». Для этого с `d162c4db` есть второй, независимый
механизм:

```
StepsController._partyCts          создаётся в Init(), отменяется в Exit()
        │  CreateLinkedTokenSource
        ▼
BattleController._battleCts        отменяется раньше партии: смерть героя,
        │                          победа (EndBattle) или выход
        ▼
BattleController.BattleToken       уходит в TargetController.StartAttack,
                                   в ApplySpell и в фазы босса
```

Правило простое: **всё, что ждёт, ждёт с токеном боя**. Циклы таймеров атаки,
тики регенерации, задержка стана, спавн зомби в третьей фазе босса — все они
прерываются одной отменой. Флаг `_isBattleOver` дополняет отмену синхронно:
он не даёт добавить юнита или засчитать конец волны уже после конца боя.

Два механизма не конкурируют: `IAction` разбирает подписки, токен гасит
незавершённую асинхронщину. Добавляешь новое ожидание — тебе нужен токен;
добавляешь новое событие — тебе нужен `ClearAction()`/`Exit()`.

### 6. Асинхронность на UniTask

Все таймеры — `UniTask.Delay`. Не корутины, не `System.Threading.Tasks`.

Причина: UniTask не аллоцирует, работает на WebGL (где нет потоков)
и интегрирован с жизненным циклом плеера.

Основной цикл — [`TargetController.TimerAttack()`](../Assets/Scripts/Runtime/Battle/TargetController.cs):
`while (_isLive && !token.IsCancellationRequested)` с постоянным шагом 100 мс
(`TIMER_STEP`). Пауза — отдельный флаг `_isPaused`, а не нулевой шаг: раньше стан
превращал `Delay` в «каждый кадр» (`d162c4db`).

Отмена ожидания везде оформлена одинаково: `UniTask.Delay(ms, cancellationToken: token)`
плюс `.SuppressCancellationThrow()` и проверка возвращённого флага — так отмена
не бросает исключение и не засоряет лог.

---

## Жизненный цикл партии

```
StartGame()
  new StepsController(uiService, book, homeController, levelConfig)
     ├─ new FlaskController(...)        ← ещё ничего не показывает
     ├─ new TableController(...)        ← ПОКАЗЫВАЕТ окно стола прямо в ctor
     └─ new BattleController(...)       ← берёт ссылку на окно боя

  stepsController.Init()
     ├─ new CancellationTokenSource()  ← токен партии
     ├─ uiService.Show<UIBattleWindow>()
     ├─ flaskController.Init()          ← создаёт колбы, показывает окно
     ├─ flaskController.SubscribeToMove()  ← ТОЛЬКО ЗДЕСЬ вешаются обработчики
     └─ battleController.Init(partyToken)  ← связанный токен боя, HP героя, первая волна
```

**Порядок всё ещё значим**, но уже не смертелен: `TableController` показывает
своё окно в конструкторе, а `FlaskController` — в `Init()`; подписки на колбы
вешаются отдельным вызовом `SubscribeToMove()` уже после `Init()`. Раньше именно
это спасало от `NullReferenceException` в
[`Flask.CheckRepits`](../Assets/Scripts/Runtime/Flask/Flask.cs) (там был
`RepitsCommand.Invoke` без `?.`) — с `d162c4db` проверка на месте, а с `fa1b425a`
вид колбы рисуется по событию модели и от порядка вызовов не зависит.

Выход из партии — `StepsController.Exit()`: отменяет токен партии, снимает
собственные подписки, гасит окна, зовёт `Exit()` у всех трёх контроллеров,
возвращает на главный экран. Связанный токен боя освобождается внутри
`BattleController.Exit()`, токен партии — сразу после.

---

## Поток данных: от клика до урона

```
клик по колбе
  → UIFlask.ButtonClickCommand
  → FlaskController.ReactClickCommand(flask)
  → MoveBall: _selectedFlask.PopElement() → flask.PushElement(element)
  → Flask.CheckRepits(): 4 одинаковых?
      → Flask.RepitsCommand(element)
      → FlaskController.OnFlaskFull(element)   + UpdateFlask (перезаполнение)
      → TableController: _currentElements.Add(element), окно показывает элемент

игрок жмёт «проверить»
  → UITableWindow.OnClickCheckCombinationButton
  → TableController.CheckRepit()
  → Table.TryGetSpell(_currentElements.ToArray(), out spellConfig)
      → успех: OnSuccessfulMerge(spellConfig.GetSpell())
      → BattleController: spell.ApplySpell(this, BattleToken)
      → например PowerAttack: battleController.HeroTarget.Health.TakeDamage(25)
      → Health.OnChanged → UIUnit.SetHealth
      → HP <= 0 → Health.Die() → OnDied → BattleController снимает юнита,
                                          освобождает позицию, проверяет конец волны
  → в любом случае: стол очищается
```

---

## Что стоит знать, прежде чем менять

1. **`Element.ID` — ключ trie.** Менять ID существующих элементов = ломать все книги.
2. **Количественные лимиты выводятся из сцены, а не дублируются в коде.** Приём
   один и тот же: код читает длину массива позиций
   (`UIBattleWindow.FriendlyPositionsCount` / `EnemyPositionsCount`,
   `UIFlaskWindow.FlaskPositionsCount`), своего числа не хранит. Раньше константа
   и сцена разъехались и роняли бой
   ([баг №1](06-known-issues.md#1--четвёртый-питомец-роняет-бой), закрыт).
   Заводишь новое ограничение такого рода — заводи его в сцене и читай длину;
   спавн сверх лимита должен вытеснять, а не падать.
3. **`Health.Die()` вызывает `Dispose()`**, который зануляет события прямо во время
   их вызова. Подписчики, добавленные после начала инвока, молча теряются —
   [баг №10](06-known-issues.md), открыт.
4. **Всё, что ждёт, ждёт с токеном боя.** Заклинание или таймер без токена
   переживает бой и продолжает работать на главном экране — это чинили
   в `d162c4db`, см. «Отмена: токен партии → связанный токен боя» выше.
5. **`Book.OnValidate` перезаписывает `_uniqElements`.** Руками это поле править
   бессмысленно — оно пересобирается из комбинаций при любом изменении книги.
