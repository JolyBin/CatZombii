# 05 — Как расширять

Практические рецепты. Все пути — от корня проекта.

> ⛔ **Закон проекта, который касается КАЖДОГО рецепта ниже: ни одной пользовательской
> строки литералом.** Ни в коде, ни в ассете, ни в TMP на сцене — только ключ.
> Рецепт — [«Добавить строку»](#добавить-строку) сразу ниже, проверка —
> `Tools → Локализация → Проверить сцену и ассеты` (Ctrl+Shift+L).

---

## Добавить строку

Самый частый и самый забываемый шаг. Забытая строка не ломает компиляцию, не роняет
игру и не видна в диффе — она просто выходит в релиз на чужом языке, и находит её
игрок. Поэтому шагов всего три, и третий обязателен.

### 1. Завести ключ в таблице

Таблица одна на проект:
[`Assets/Resources/Localization/Localization Table.asset`](../Assets/Resources/Localization/Localization%20Table.asset)
(быстро открыть — `Tools → Локализация → Выделить таблицу строк`).

Добавь строку: **Key**, **Russian**, **English**.

| Колонка | Что писать |
|---|---|
| `Key` | `<экран>.<элемент>`, нижний регистр, слова через `_`. Например `home.play`, `win.claim`, `spell.power_attack` |
| `Russian` | **Заполняется всегда.** Русский — язык-источник, на него падает фолбэк |
| `English` | **Оставляй пустым.** EN — фаза 2 ([09](09-platform-strategy.md)); пустая ячейка не ошибка, строка уйдёт игроку по-русски |

Соглашение по префиксам — по экрану, а не по типу: `home.`, `heroes.`, `equipment.`,
`win.`, `lose.`, `pause.`, `battle.`, `table.`; для данных — `spell.`, `unit.`,
`hero.<герой>.name` / `hero.<герой>.class`.

### 2. Использовать ключ

**Из сцены или префаба** — компонент `LocalizedText` на объекте с TMP:

| Поле | Значение |
|---|---|
| `Key` | ключ из таблицы |
| `Source` | `Key` — строка берётся из таблицы |
| | `Code` — текст ставит код (имя героя, номер волны). Компонент всё равно нужен: он объявляет намерение, иначе проверка не отличит «осознанно» от «забыли» |
| | `Placeholder` — заглушка чужого UI-кита, переводу не подлежит |
| `Text` | ссылка на TMP; проставляется сама при добавлении компонента |

**Из кода** — через ключ-константу в
[`LocKeys`](../Assets/Scripts/Runtime/Utility/Services/Localization/LocKeys.cs):

```csharp
_waveText.text = Localization.Get(LocKeys.BattleWave, currentWave, maxWave);
```

Константа, а не строка по месту: опечатка в литерале — это не ошибка компиляции,
а маркер `#battle.wav#` в готовой игре.

**Из ассета** — поле-ключ, а не поле-текст: `_nameKey` у `BaseSpellConfig`
и `UnitConfig`, `_nameHeroKey` / `_classHeroKey` у `Book`.

### 3. Проверить, что ничего не забыто

`Tools → Локализация → Проверить сцену и ассеты` (**Ctrl+Shift+L**). Один отчёт в консоль:

- **ошибка** — литерал в сцене на активном объекте, литерал в префабе, ключ без строки
  в таблице, пустой ключ, пустой русский. Ошибки — это то, что увидит игрок;
- **предупреждение** — мёртвый UI (выключенный объект с литералом: подключить
  или удалить), ключи-сироты, пропущенный префаб с потерянным скриптом;
- **справка** — сколько ещё не переведено на английский и сколько заглушек кита осталось.

Проверка ходит по сцене, **по префабам в `Resources/`** и по ассетам. Префабы здесь
не для полноты: юниты боя и карточки создаются в рантайме, их текст не виден
ни в одном осмотре сцены — именно так в проекте выжил литерал «BOSS» на каждом зомби,
и нашёлся он только прогоном игры.

### Как это устроено внутри

```
Localization (статический фасад)
   └─ LocalizationService (POCO)
        ├─ LocalizationTable (ScriptableObject в Resources/)
        └─ ILanguageSource[]  ← ОТКУДА язык, по приоритету:
             1. PlayerChoiceLanguageSource  выбор игрока (PlayerPrefs)
             2. PluginYGLanguageSource      площадка, за #if
             3. SystemLanguageSource        язык системы/браузера
             ─ ни один не ответил → русский
```

Приоритет задан в одном месте — `Localization.CreateDefault()`.

**PluginYG — только источник языка и ничего больше.** Требование
[09](09-platform-strategy.md): SDK-слой изолирован, иначе мобилки и Steam дорожают.
Файл `PluginYGLanguageSource.cs` можно удалить целиком — ничего не сломается.
Он закрыт **двойным** `#if PLUGIN_YG_2 && Localization_yg`, потому что дефайн
`PLUGIN_YG_2` в проекте стоит, а **модуль локализации PluginYG не установлен**:
guard только по `PLUGIN_YG_2` уронил бы компиляцию.

**Отсутствующий ключ не превращается в пустоту.** `Get` вернёт заметный маркер
`#ключ#` и один раз (не каждый кадр) напишет ошибку в консоль. Пустая строка в UI —
дефект, который находит игрок; маркер находит разработчик.

**Момент установки текста.** Окна гасятся `Canvas.enabled`, а не `SetActive`
([04, решение 4](04-architecture.md)), поэтому `OnEnable` у текста внутри окна
**при показе окна не сработает** — он отработает один раз, при загрузке сцены.
Вторую точку даёт `UIWindow.Show()` → `ApplyLocalization()`: окно проходит по своим
`LocalizedText` и просит каждый обновиться. Обе точки нужны: `OnEnable` закрывает
объекты, созданные пулом в рантайме, `Show()` — смену языка при открытом окне.

---

## Добавить заклинание

Самая частая задача. Шаблон отработан на Warrior'е — восемь заклинаний
Wizard/Enchantress пишутся ровно по нему.

### 1. Код

Создай файл в `Assets/Scripts/Runtime/Spells/Models/<Герой>/`.
Один файл содержит **и конфиг, и рантайм-класс** — так принято в проекте.

```csharp
using Core.Battle;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Ice Arrow Config",
                     menuName = "Spells/Wizard/Create Ice Arrow Config")]
    public class IceArrowConfig : BaseSpellConfig
    {
        [SerializeField] private int _damage;
        public override BaseSpell GetSpell() => new IceArrow(_damage);
    }

    public class IceArrow : BaseSpell
    {
        private readonly int _damage;
        public IceArrow(int damage) => _damage = damage;

        public override UniTask ApplySpell(BattleController battleController,
                                           CancellationToken token)
        {
            if (battleController.EnemySquad.Length == 0)
                return UniTask.CompletedTask;   // ← ОБЯЗАТЕЛЬНО: иначе NRE
            UnitRuntime primaryTarget = battleController.HeroTarget;
            if (primaryTarget == null)
                return UniTask.CompletedTask;   // ← цель могла умереть до применения

            primaryTarget.Health.TakeDamage(_damage);
            return UniTask.CompletedTask;
        }
    }
}
```

Сигнатура — `UniTask ApplySpell(BattleController, CancellationToken)`, см.
[`BaseSpellConfig.cs`](../Assets/Scripts/Runtime/Spells/Models/BaseSpellConfig.cs).
Мгновенное заклинание не объявляется `async`: оно делает работу и возвращает
`UniTask.CompletedTask` — образец
[`PowerAttackConfig.cs`](../Assets/Scripts/Runtime/Spells/Models/Warrior/PowerAttackConfig.cs).

**Обязательно проверяй `EnemySquad.Length == 0` перед обращением к `HeroTarget`,
а сам `HeroTarget` — на `null`** — иначе `NullReferenceException`, когда волна уже
зачищена, а заклинание долетело.

### 1a. Заклинание с задержкой: токен обязателен

Если внутри есть `await` — тик, стан, отложенный удар, — токен надо пробросить
в каждое ожидание и проверять между тиками. Образец —
[`RegenirationConfig.cs`](../Assets/Scripts/Runtime/Spells/Models/Warrior/RegenirationConfig.cs):

```csharp
public override async UniTask ApplySpell(BattleController battleController,
                                         CancellationToken token)
{
    int count = 0;
    while (count < _count)
    {
        if (token.IsCancellationRequested)
            return;
        battleController.HeroHealth.Heal(_healthvalue);
        count++;

        bool isCanceled = await UniTask.Delay(_timer, cancellationToken: token)
                                       .SuppressCancellationThrow();
        if (isCanceled)
            return;                     // ← бой кончился раньше заклинания
    }
}
```

**Зачем токен.** Он живёт ровно столько же, сколько бой: `StepsController` держит
токен партии, `BattleController` создаёт связанный с ним токен боя и отменяет его
при смерти героя, победе и выходе на главный экран. Всё, что заклинание делает
после `await`, обязано прерываться по этому токену.

**Что будет, если не пробросить.** Цикл переживёт бой: регенерация продолжит лечить
героя уже на главном экране, стан позовёт `ContinueAttack()` на уничтоженной цели,
отложенный спавн добавит юнита в мёртвый бой. Именно это чинили в `d162c4db` —
не повторяй. В WebGL при `Exception Support = Explicitly Thrown Only` такие
исключения ещё и глотаются молча: игрок видит странности, разработчик не видит ничего.

`SuppressCancellationThrow()` возвращает флаг вместо броска `OperationCanceledException` —
так отмена не засоряет лог. Если бросить исключение всё же нужно, оно перехватывается
единственной точкой запуска в `BattleController` и гасится тихо; любое **другое**
исключение уходит в `Debug.LogException`.

### 2. Ассет

`Create → Spells → <Герой> → ...` — положи в
`Assets/Resources/Spells/Books/<Герой> Book/`. Заполни параметры и `Name Key`.

> ⛔ **`Name Key` — это КЛЮЧ, а не текст.** Соглашение: `spell.<имя_заклинания>`.
> Саму строку заведи в таблице (см. [«Добавить строку»](#добавить-строку)).
> Имя заклинания печатается игроку дважды — в окне варки (`UITableWindow.ShowResult`)
> и в карточке комбинации, — так что литерал здесь виден сразу.

### 3. Комбинация

`Create → Spells → Create Combination`. Задай `_elements` — **упорядоченный**
массив элементов — и ссылку на конфиг заклинания.

### 4. Подключить к книге

Добавь комбинацию в `_combinations` у книги. `OnValidate` сам пересоберёт
`_uniqElements` — набор элементов, которые герой увидит в колбах.

### Что доступно из `BattleController`

| Член | Что даёт |
|---|---|
| `HeroHealth` | HP героя: `TakeDamage`, `Heal`, `PercentDamage` |
| `HeroTarget` | Выбранный игроком враг (`UnitRuntime`) |
| `EnemySquad` | Копия массива живых врагов |
| `FriendlySquad` | Копия массива живых союзников |
| `AddEnemy(UnitConfig)` | Заспавнить врага |
| `AddFriend(UnitConfig)` | Заспавнить союзника |
| `BattleToken` | Токен жизни боя — тот же, что приходит в `ApplySpell` |

Сколько одновременно живых юнитов помещается в бой, решает **сцена**: лимит равен
длине массива позиций в `UIBattleWindow` (`FriendlyPositionsCount` /
`EnemyPositionsCount`). Сегодня это 3 союзника и 4 врага; добавил позицию в сцену —
лимит вырос сам, править код не нужно. Спавн сверх лимита не падает, а вытесняет
уже стоящего юнита — правило вытеснения у сторон разное,
см. [баг №16](06-known-issues.md).

Через `unit.TargetController` доступны `StopAttack()` / `ContinueAttack()` /
`SetNewTimerValue(ms)` — так сделаны стан и фазы босса.

### Механики, которых пока нет

Для Wizard/Enchantress понадобится дописать:

- **Замедление** — есть `SetNewTimerValue`, но нужно возвращать исходное значение;
  сейчас никто этого не делает.
- **Прямое лечение** — есть только `Regeniration` тиками. `HeroHealth.Heal(n)` готов.
- **Зачарование/подчинение** — механики перехода юнита между `_enemyList`
  и `_friendlyList` нет вообще, придётся проектировать.

---

## Добавить героя

1. `Create → Spells → Create Book`, положи в `Assets/Resources/Spells/Books/<Имя> Book/`.
2. Заполни `Name Hero Key`, `Class Hero Key`, `HP`, `IconClass`, `HeroIcon`.

   > ⛔ Оба поля — **ключи**: `hero.<герой>.name` и `hero.<герой>.class`.
   >
   > **Класс** (`Воин`, `Маг`, `Некромант`, `Чародейка`) — обычный игровой термин,
   > переводится безусловно: «Warrior» в русской игре — тот же дефект, что «Wave: 1/3».
   >
   > **Имя** тоже идёт через ключ, хотя это имя собственное. Причина не в переводе,
   > а в письменности: целями фазы 3 [09](09-platform-strategy.md) называет упрощённый
   > китайский и японский, а имя собственное в них не остаётся латиницей — оно
   > транслитерируется. Ключ это позволяет и стоит ноль. Русские значения сегодня —
   > та же латиница, что и была (`Sokushi`, `Qwas`, `Immortal`, `Zlaxxalz`):
   > переименование героев — решение владельца, а не побочный эффект локализации.
3. Создай заклинания и комбинации (см. выше), добавь их в `_combinations`.
4. **В сцене**: на экране выбора героя добавь карточку `UIHero` и положи книгу
   в её поле `_heroBook`. Сейчас там 4 карточки.
5. Проверь, что `_uniqElements` заполнился после `OnValidate` — иначе колбы
   будут пустыми.

> Герой без работающих заклинаний хуже, чем отсутствие героя: игрок выберет его
> и получит исключение. Либо реализуй, либо скрой карточку.

---

## Добавить юнита

1. **Атака**: `Create → Battle Configs → Create Single Attack Config`, задай урон.
   Для сложного поведения — свой класс по образцу
   [`BossSpellConfig.cs`](../Assets/Scripts/Runtime/Battle/Attack%20Configs/BossSpellConfig.cs).
2. **Префаб**: скопируй существующий из `Assets/Resources/UI/Core/Pfrefabs/Enemy/`.
   На корне должен быть компонент `UIUnit` со всеми заполненными ссылками
   (имя, полоски HP и таймера, кнопка выбора).
   На каждом TMP внутри префаба обязан быть `LocalizedText` — у юнитов это
   `Source = Code` (текст ставит `UIUnit`). Префаб — самое опасное место для литерала:
   юнит создаётся в рантайме, и осмотр сцены его текста не видит.
3. **Конфиг**: `Create → Battle Configs → Create Unit Config`.

| Поле | Смысл |
|---|---|
| `Name Key` | **Ключ** подписи юнита (`unit.<юнит>`), не текст. **Пусто = юнит без подписи** — так у всех рядовых зомби, и это законно, в отличие от пропавшего ключа |
| `HP` | Здоровье |
| `AttackCooldown` | Мс между атаками. **`0` = юнит не атакует** |
| `TargetPriority` | Кого враги бьют охотнее. Больше = приоритетнее. Герой = 0 |
| `AttackConfig` | Ссылка на конфиг атаки |
| `UnitPrefab` | Ссылка на префаб с `UIUnit` |

> `AttackCooldown = 0` — юнит без атаки. Раньше такой юнит ронял игру при смерти
> ([баг №3](06-known-issues.md#3--unitruntimedispose--мина)), сейчас это починено
> (`d162c4db`): пассивных юнитов — щит, тотем, декоративного союзника — делать можно.
> Учти, что таймер у него скрыт, а `AttackConfig` не создаётся вовсе.

---

## Добавить уровень

1. `Create → Battle Configs → Create Level Config`, положи в `Assets/Resources/Battle/Level 1/`.
2. Задай `Background` и массив `Waves`. У каждой волны — `HealValue`
   (лечение героя перед волной) и массив `UnitConfigs`.
3. **Обязательно**: добавь уровень в массив `_currentLevels` у `GameManager`
   на объекте `Main Camera` в сцене. Без этого уровень недостижим.

Порядок в `_currentLevels` = порядок прохождения.

---

## Добавить окно

1. Класс наследуй от `UIWindow` (или `UISimpleWindow` / `UISimpleClosableWindow` /
   `UISimpleQuestionWindow`).
2. На объекте обязателен `Canvas` — `UIWindow` помечен `[RequireComponent(typeof(Canvas))]`.
   Поле `_canvas` заполняется автоматически в `Reset()`.
3. **Объект должен быть активен на сцене при старте** — `UIService` собирает окна
   через `FindObjectsByType<UIWindow>(FindObjectsInactive.Exclude)` в `Awake`.
   Прятать через `SetActive(false)` в инспекторе нельзя, окно просто не найдётся.
4. Показ/скрытие: `uiService.Show<MyWindow>()` / `Hide<MyWindow>()` / `Get<MyWindow>()`.
5. В `Hide()` **обязательно** отписывайся: `button.onClick.RemoveAllListeners()`
   и `MyEvent = null`. Образец — `UIBattleWindow.Hide()`.
6. ⛔ **Ни одной пользовательской строки литералом в этом окне.** На каждом TMP —
   компонент `LocalizedText`: `Key` для текста из таблицы, `Code` для текста, который
   ставит сам класс окна. Тексты, набитые прямо в TMP «на время», временными не бывают:
   они выходят в релиз, потому что ничего не ломают и не видны в диффе.
   Переопределяешь `Show()` — **зови `base.Show()`**: именно там окно обновляет свои
   строки (`ApplyLocalization()`). Если `base.Show()` не зовётся (как в `UISimpleWindow`),
   позови `ApplyLocalization()` руками.
7. Закончил — прогони `Tools → Локализация → Проверить сцену и ассеты` (Ctrl+Shift+L).
   Отчёт должен быть без ошибок.

---

## Правила, которые легко нарушить

0. ⛔ **НИ ОДНОЙ ПОЛЬЗОВАТЕЛЬСКОЙ СТРОКИ ЛИТЕРАЛОМ.** Ни `"Не сварилось"` в коде,
   ни `Name = "Царап"` в ассете, ни текст, набитый прямо в TMP на сцене или в префабе.
   Только ключ — см. [«Добавить строку»](#добавить-строку).

   Это правило нарушается легче всех остальных, потому что нарушение **ничего
   не ломает**: игра компилируется, запускается и работает. Она просто говорит
   с игроком не на его языке — и первым это замечает игрок, а не разработчик.
   В проекте это уже случилось дважды: `UIBattleWindow.SetWave` печатал английское
   `Wave: 1/3` в русской игре, а на каждом префабе зомби висел литерал `BOSS`,
   который не видно в сцене вовсе — юнит создаётся в рантайме.

   Ловится одной кнопкой: **`Tools → Локализация → Проверить сцену и ассеты`
   (Ctrl+Shift+L)**. Прогоняй её перед тем, как считать работу над UI законченной;
   отчёт обязан быть без ошибок.

1. **Файлы с кириллицей сохраняй в UTF-8 (лучше с BOM).** Три файла проекта раньше
   были в CP1251, и русские строки превращались в мусор при сборке. Исправлено,
   но Visual Studio может сохранить обратно в ANSI, если BOM потеряется.
2. **Не меняй `Element.ID` существующих элементов** — это ключи trie, сломаются
   все книги разом.
3. **Каждой подписке — свой `ClearAction()`/`Exit()`.** Иначе утечка между боями.
4. **Проверяй пустой `EnemySquad`** в заклинаниях — и `HeroTarget` на `null`.
5. **Пробрасывай `CancellationToken` в каждое ожидание.** Заклинание без токена
   переживает бой — см. раздел «Добавить заклинание».
6. **Лимиты состава выводятся из сцены**, а не задаются в коде: длина массивов
   позиций в `UIFlaskWindow` и `UIBattleWindow`. Хочешь другой лимит — меняй сцену.
7. Правка `Assets/Plugins/UniTask/Editor/UniTaskTrackerTreeView.cs` — локальная
   (совместимость с Unity 6.5). При обновлении UniTask её надо накатить заново,
   см. [08 — Инструменты](08-tooling.md).
