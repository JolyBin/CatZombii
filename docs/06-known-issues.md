# 06 — Известные проблемы

*Срез: 1 августа 2026. Найдено чтением кода и сверкой с живой сценой.
Ничего из этого не исправлено.*

Отсортировано по достижимости в реальной игре.

---

## 1 — Четвёртый питомец роняет бой

**Где:** [`BattleController.cs:120-141`](../Assets/Scripts/Runtime/Battle/BattleController.cs#L120)
+ `UIBattleWindow._friendlyPositions` в сцене

**Суть:** код разрешает до 4 живых союзников, а в сцене под них только **3** слота.

```csharp
public void AddFriend(UnitConfig unit)
{
    if (_friendlyList.Count == 4)     // ← вытеснение только при 4
        _friendlyList[0].Health.Die();
    ...
    UIUnitPosition unitPosition = _battleWindow.SetFriendPosition();
    // SetFriendPosition() = _friendlyPositions.First(x => x.IsFree)
    // при 3 занятых слотах → InvalidOperationException
}
```

**Воспроизведение:** играть за Necromancer'а, призвать четвёртого питомца до того,
как умрёт первый. Например: `Dark > Venom` ×2, затем `Dark > Venom > Dark > Venom`
(призывает сразу двоих).

У врагов такой проблемы нет — там 4 слота и лимит 4 совпадают.

**Варианты правки:**
- заменить `== 4` на `== 3` в `AddFriend` (быстро, но константа снова зашита);
- добавить 4-й слот в сцену (сохраняет задуманный лимит);
- лучше: брать лимит из `_friendlyPositions.Length`, чтобы код и сцена не расходились.

---

## 2 — Генератор элементов исчерпаем

**Где:** [`ElementsGenerator.cs:43-53`](../Assets/Scripts/Runtime/Flask/ElementsGenerator.cs#L43)

```csharp
int rndIndex = Random.Range(0, _elemntIndexes.Count);
int elementIndex = _elemntIndexes[rndIndex];
...
_elemntIndexes.RemoveAt(rndIndex);   // ← никогда не возвращается обратно
```

Пул — 500 индексов, создаётся в `FlaskController.Init()`. Каждое схлопывание колбы
забирает 4. Примерно **125 схлопываний за бой**, дальше `Random.Range(0, 0)`
возвращает 0, обращение к пустому списку → `ArgumentOutOfRangeException`.

Пул пересоздаётся на каждый бой, так что мгновенной смерти нет. Но затяжной уровень
(особенно босс с 450 HP) до лимита дожить может.

**Правка:** либо возвращать индекс в пул после выдачи, либо не удалять вовсе
(`RemoveAt` тут вообще не нужен для равномерности), либо перезаполнять пул при опустошении.

---

## 3 — `UnitRuntime.Dispose()` — мина

**Где:** [`UnitRuntime.cs:44-51`](../Assets/Scripts/Runtime/Battle/UnitRuntime.cs#L44)

```csharp
public UnitRuntime(UnitConfig unit, BattleController battleController)
{
    ...
    if (unit.AttackCooldown > 0)
        _unitSpell = unit.AttackConfig.GetUnitSpell();   // ← только здесь
}

public void Dispose()
{
    ...
    _unitSpell.DisposeSpell();    // ← безусловно
}
```

Юнит с `AttackCooldown = 0` не получает `_unitSpell`, но `Dispose()` всё равно
его дёргает → `NullReferenceException` при смерти.

**Сейчас не стреляет:** у всех шести юнитов в проекте кулдаун ненулевой (1500–5000).
Но первый же пассивный юнит — щит, тотем, декоративный союзник — уронит игру.

**Правка:** `_unitSpell?.DisposeSpell();`

---

## 4 — Переполнение стола

**Где:** [`UITableWindow.cs:50-56`](../Assets/Scripts/Runtime/Spells/UI/UITableWindow.cs#L50)

```csharp
public void ShowFullFlask(Sprite sprte)
{
    UIFullFlask newFlask = _flasks[_curretnEmptyPositions];   // ← без проверки границ
    _curretnEmptyPositions++;
```

На столе 6 слотов. Собрал 7 элементов, не нажав «проверить» → `IndexOutOfRangeException`.

Самая длинная комбинация в игре — 4 элемента, так что копить 7 незачем.
Но игрок этого не знает, и ничто его не останавливает.

**Правка:** либо игнорировать элементы сверх лимита, либо (лучше) автоматически
проверять комбинацию при заполнении стола, либо блокировать схлопывание колб
при полном столе.

---

## 5 — Успех и провал комбинации выглядят одинаково

**Где:** [`UITableWindow.cs:67-70`](../Assets/Scripts/Runtime/Spells/UI/UITableWindow.cs#L67)

```csharp
public void ShowResult(bool result, string name)
{
    _resultMergeText.text = name;   // ← result не используется вообще
}
```

При провале `TableController` передаёт строку «Ничего не получилось((»,
при успехе — название заклинания. Оба текста рисуются одинаково: нет цвета,
нет звука, нет анимации различия.

Это **не крэш, а дизайнерская дыра**: игрок не понимает, попал он или слил элементы.

**Правка:** использовать `result` — цвет текста, разная анимация, звук.

---

## 6 — `RepitsCommand.Invoke` без null-проверки

**Где:** [`Flask.cs:59`](../Assets/Scripts/Runtime/Flask/Flask.cs#L59)

```csharp
_stack.Clear();
RepitsCommand.Invoke(firstElement);   // ← должно быть RepitsCommand?.Invoke
```

Сейчас спасает только порядок инициализации: `FlaskController.Init()` наполняет
колбы через конструктор и `UpdateFlask()` (которые не вызывают `CheckRepits`),
а подписка происходит позже в `SubscribeToMove()`.

Поменяется порядок — получишь `NullReferenceException`. Хрупко на ровном месте.

**Правка:** `RepitsCommand?.Invoke(firstElement);`

---

## 7 — Стан вместо паузы жжёт CPU

**Где:** [`TargetController.cs:31-39, 99-112`](../Assets/Scripts/Runtime/Battle/TargetController.cs#L31)

```csharp
public void StopAttack()  => _timerStep = 0;
...
while (_isLive)
{
    CurrentTimer = Math.Clamp(CurrentTimer - _timerStep, 0, _startTimer);
    ...
    await UniTask.Delay(_timerStep);   // ← Delay(0) на время стана
}
```

`_timerStep = 0` означает и «таймер не идёт» (логически верно), и `UniTask.Delay(0)` —
то есть цикл начинает крутиться на каждом кадре вместо раза в 100 мс.

Стан работает, но на время стана юнит жжёт CPU. На слабом железе в WebGL при
нескольких застаненных юнитах это заметно.

**Правка:** развести «шаг таймера» и «пауза» — отдельный флаг `_isPaused`,
а `Delay` всегда с константой 100 мс.

---

## 8 — `SelectEnemyTarget` не проверяет текущую цель

**Где:** [`BattleController.cs:152-165`](../Assets/Scripts/Runtime/Battle/BattleController.cs#L152)

```csharp
private void SelectEnemyTarget(UnitRuntime unitRuntime)
{
    HeroTarget.UIUnit.Selected(false);   // ← HeroTarget может быть null
    ...
}

private void SelectedLastTarget()
{
    if (_enemyList.Count == 0)
        return;                          // ← выходит, оставляя старую ссылку
    ...
}
```

`SelectedLastTarget` при пустом списке врагов оставляет `HeroTarget` протухшим.
Последующий клик по врагу новой волны обратится к уничтоженному объекту.

**Правка:** занулять `HeroTarget` при пустом списке и проверять на null в `SelectEnemyTarget`.

---

## 9 — Wizard и Enchantress бросают исключение

**Где:** [`Spell.cs`](../Assets/Scripts/Runtime/Spells/Models/Spell.cs)

```csharp
public override BaseSpell GetSpell()
{
    throw new System.NotImplementedException();
}
```

Все 8 заклинаний этих двух героев висят на этом классе-заглушке. Выбрал героя,
собрал комбинацию, нажал «проверить» → исключение.

Это **не баг, а незаконченный контент** — но для игрока неотличимо от поломки.
Подробности в [03 — Контент](03-content.md), план в [07 — Роадмап](07-roadmap.md).

**Временная мера до реализации:** скрыть карточки этих героев на экране выбора.

---

## 10 — События зануляются во время собственного вызова

**Где:** [`Health.cs:76-80`](../Assets/Scripts/Runtime/Battle/Health.cs#L76)

```csharp
public void Die()
{
    OnDied?.Invoke();
    Dispose();        // ← зануляет OnDied, OnChanged и остальные
}
```

Работает, потому что `Invoke` уже скопировал список подписчиков. Но подписчик,
добавленный *внутри* обработчика `OnDied`, молча потеряется.

Не проявляется в текущем коде, но это ловушка при добавлении реакций на смерть.

---

## 11 — Питомцы не останавливаются при смерти героя → победа поверх поражения

**Где:** [`BattleController.cs:56-63`](../Assets/Scripts/Runtime/Battle/BattleController.cs#L56)

```csharp
HeroHealth.OnDied += () =>
{
    OnHeroDie?.Invoke();
    foreach (var item in _enemyList)      // ← только враги
    {
        item.TargetController.StopAttack();
    }
};
```

`StopAttack()` рассылается только по `_enemyList`. Питомцы Necromancer'а из `_friendlyList`
продолжают бить. Если они добьют последнюю волну уже после смерти героя, сработает
`OnAllEnemyDie` (строки 105–111) и **окно победы откроется поверх окна поражения**.

**Воспроизведение:** играть за Necromancer'а, призвать питомцев, умереть на последней
волне при живых врагах с малым HP.

**Правка:** останавливать обе стороны, плюс снимать подписку на `OnAllEnemyDie` после
смерти героя.

---

## 12 — Пазл остаётся интерактивным под окном поражения

**Где:** [`StepsController.cs:68-76`](../Assets/Scripts/Runtime/Steps/StepsController.cs#L68)

`ShowLoseWindow` просто показывает окно. `FlaskController` и `TableController` остаются
подписанными и живыми — игрок может продолжать переливать шарики и варить заклинания,
пока висит «ты проиграл». То же и с окном победы.

Кроме того, `_isLive` у врагов остаётся `true`, а `StopAttack()` выставляет
`_timerStep = 0` → `UniTask.Delay(0)` крутится каждый кадр **для каждого врага** вплоть
до нажатия «Продолжить». То есть [баг №7](#7--стан-вместо-паузы-жжёт-cpu) в момент
поражения умножается на число врагов.

Оба симптома — следствие того, что **паузы боя в игре не существует**. Она всё равно
нужна для рекламы и для сворачивания мобильного браузера
(см. [09](09-platform-strategy.md), скоуп фазы 1).

---

## 13 — Отмены не существует нигде: заклинания живут дольше боя

**Где:** [`BaseSpellConfig.cs`](../Assets/Scripts/Runtime/Spells/Models/BaseSpellConfig.cs),
все `ApplySpell`

`ApplySpell` объявлен `async void`, а `CancellationToken` не встречается в проекте **ни разу**.
Последствия уже есть в текущем контенте:

- `Regeniration.ApplySpell` крутит цикл с `UniTask.Delay(2000)` и продолжает лечить
  `HeroHealth` **после** `StepsController.Exit()`;
- `FastAttackSpell` ждёт `_stunTimer * 1000` мс и потом зовёт `ContinueAttack()` —
  цель к этому моменту может быть мертва и `Dispose()`-нута.

При Exception Support = «Explicitly Thrown Only» в WebGL это глотается молча: игрок видит
странное поведение, разработчик не видит ничего. Каждый новый таймер (часы котла, пакости)
умножает число осиротевших циклов.

**Правка:** `CancellationTokenSource` на партию, `ApplySpell(BattleController, CancellationToken)`.

---

## 14 — `UIFlaskWindow` течёт шестью объектами за бой

**Где:** [`UIFlaskWindow.cs:32`](../Assets/Scripts/Runtime/Flask/UI/UIFlaskWindow.cs#L32)

`Show()` создаёт **новый** `Pool<UIFlask>` при каждом показе, а `Pool.PopulatePool()`
инстанциирует объекты на всю `Capacity`. Старые шесть `UIFlask` остаются в контейнере
навсегда. За 3–5 партий терпимо, при длинной сессии — нет.

---

## 15 — У колбы два источника истины о содержимом

**Где:** [`UIFlask.cs`](../Assets/Scripts/Runtime/Flask/UI/UIFlask.cs) (`_currentIndex`)
против [`Flask.cs`](../Assets/Scripts/Runtime/Flask/Flask.cs) (`_stack`)

`UIFlask` ведёт собственный счётчик `_currentIndex` параллельно `Flask._stack`. Сегодня они
синхронны только потому, что колбу двигает **единственный** путь — клик игрока. Любой новый
путь мутации (часы котла, пакость, повадка кота, автопереливание) рассинхронит их молча.

Сопутствующее ограничение модели: колбы лежат в `Dictionary<Flask, UIFlask>`, то есть
**у колбы нет ни индекса, ни порядка**, а наружу `Flask` отдаёт только
`IsPossiblePushElement` / `IsPossiblePopElement` — узнать, сколько в ней одинаковых,
невозможно. Любая механика, целящаяся «в самую полную колбу», требует рефактора модели.

**Правка:** единый путь мутации, `UIFlask` перестаёт вести свой счётчик и становится
чистым отображением.

---

## Предупреждения компилятора

После апгрейда на Unity 6.5 проект собирается **без ошибок**
(проверено в живом редакторе: `recompile_status → completed, errors: []`).

Остались предупреждения:

| Код | Где | Что |
|---|---|---|
| `CS0649` | ~25 полей в UI-классах | `[SerializeField]`-поля без присваивания в коде — норма для Unity |
| `CS1998` | конфиги заклинаний | `async` без `await` — следствие сигнатуры `ApplySpell` |
| `CS4014` | `TargetController.cs:53` | `TimerAttack()` не ожидается — намеренный fire-and-forget |
| `CS0169` | `UIBattleWindow._currentFriendlyPositionIndex` | Неиспользуемое поле, можно удалить |
| `CS0618` | `Assets/PluginYourGames/Example/` | Устаревший API в демо-сценах вендора. Папку `Example` можно удалить целиком |

Ни одно из них не блокирует сборку.
