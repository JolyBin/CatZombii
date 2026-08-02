# CatZombii (ZombiiCat)

Unity 2D-игра: смесь пазла «переливание шариков по колбам» и пошагового автобоя.
Целевая платформа — **WebGL для Яндекс.Игр** (PluginYG 2).

## 📖 Полная документация — в [`docs/`](docs/README.md)

Прежде чем что-то менять, загляни туда — там разобрано, **почему** система устроена
так, а не иначе, и что уже сломано.

| Задача | Документ |
|---|---|
| Впервые видишь проект | [docs/01-overview.md](docs/01-overview.md) |
| Геймдизайн, баланс, открытые вопросы | [docs/02-game-design.md](docs/02-game-design.md) |
| Точные цифры контента (элементы, книги, юниты, уровни) | [docs/03-content.md](docs/03-content.md) |
| Архитектура и ключевые решения | [docs/04-architecture.md](docs/04-architecture.md) |
| Как добавить заклинание/героя/юнита/уровень | [docs/05-extending.md](docs/05-extending.md) |
| Известные баги с правками | [docs/06-known-issues.md](docs/06-known-issues.md) |
| Что мешает релизу | [docs/07-roadmap.md](docs/07-roadmap.md) |
| Unity CLI, MCP, апгрейд, сборка | [docs/08-tooling.md](docs/08-tooling.md) |
| Куда выпускать, стратегия и гейты | [docs/09-platform-strategy.md](docs/09-platform-strategy.md) |
| **Принятый дизайн игры** (ядро, прогрессия, мета, экономика) | **[docs/10-progression-design.md](docs/10-progression-design.md)** |
| Арт: стиль, ассеты, лицензии, смета | [docs/11-art-direction-and-assets.md](docs/11-art-direction-and-assets.md) |
| Композиция экрана, читаемость, оживление поля | [docs/12-game-field-and-ui.md](docs/12-game-field-and-ui.md) |

**Быстрые факты, которые чаще всего нужны:** проект компилируется без ошибок;
Wizard и Enchantress — заглушки, бросают `NotImplementedException`; все 7 уровней
собраны и подключены к сцене; **сохранения есть** (свой слой, `PlayerPrefs` + носитель
под Яндекс, но модуль Storage у PluginYG не установлен, поэтому облако выключено);
остальной интеграции с Яндекс.Играми нет — ни рекламы, ни SDK.

**Важно про статус документации.** 01.08.2026 владелец снял ограничение скоупа, и игра
переработана:

- **Ядро — знание книги, а не скорость рук.** Рецепты отличаются *назначением*,
  а не величиной. Часы котла как ядро отменены владельцем в тот же день.
- **Мир пошаговый.** Один перелив = один такт; враги ходят, только когда ходишь ты.
  Варка тоже стоит такта.
- **Элементы в котле стынут по правилу хвоста:** тикает только последний положенный
  элемент, он же и улетает. Срок — **N тактов**, не секунд; в сцене сейчас 8
  (`UITableWindow._coolingTacts`).
- **Мета принята** (docs/10 §13): карта из 15 узлов, боссы на 5/10/15, узлы
  перепроходимы; **три героя** открываются прогрессом, не валютой; валюта покупает
  заклинания и слоты колоды (4→8); **семь стихий, восьмого элемента не будет**.

Документы 02 и 03 описывают **прототип как он есть**, документ 10 — **принятый
дизайн**. При расхождении прав документ 10.

⚠️ **Вёрстка контента разблокирована** (такт схлопывания замерен — 5,0 с), но
**калибровка открыта**: замер «переливов на схлопывание» дал 3,5 (медиана 3,0) при двух
стихиях, из чего срок остывания выходит 4–7 тактов против стоящих в сцене 8, а бестиарий
docs/10 §16 сверстан при допущении 6 переливов. Осмысленных сессий пока две, обе
на уровне 1. Прежде чем верстать волны и HP — читай docs/10 §8, шаг 0.

## Окружение

- **Unity 6000.5.3f1** (Unity 6.5). Проект был поднят с Unity 2022 — см. «История апгрейда».
- Целевая платформа сборки: **WebGL**, шаблон `PROJECT:YandexGames` (`Assets/WebGLTemplates/YandexGames`).
- Единственная сцена в билде: `Assets/Scenes/SampleScene.unity`.
- Скриптовые дефайны для WebGL: `DOTWEEN;PLUGIN_YG_2;TMP_YG2;RU_YG2;YandexGamesPlatform_yg`.

### Проверка компиляции без запуска Editor

Unity держит блокировку на проекте, поэтому batch-mode прогнать нельзя, пока открыт Editor.
Быстрая проверка — собрать сгенерированные Unity `.csproj` напрямую Roslyn'ом:

```bash
CSC="/c/Program Files/dotnet/sdk/9.0.304/Roslyn/bincore/csc.dll"
# из <csproj> берутся <DefineConstants>, <Compile Include>, <HintPath>, <ProjectReference>
# и превращаются в response-файл для csc; порядок сборки:
# UniTask -> UniTask.{Linq,Addressables,DOTween,TextMeshPro,Editor} -> Assembly-CSharp -> Assembly-CSharp-Editor
dotnet "$CSC" "@<name>.rsp"
```

Ошибки компиляции Unity также лежат в `Logs/Editor.log` (ищи `error CS`).

## Структура

```
Assets/
  Scripts/
    Editor/Utility/           ImageAnchorsUtility — редакторная утилита
    Runtime/
      GameManager.cs          точка входа (MonoBehaviour на сцене)
      Meta/                   мета-слой: HomeController, HeroController, EquipmentController + окна
      Steps/                  StepsController — склейка боя, колб и стола; WorldClock (часы мира)
      Flask/                  пазл-механика колб: Flask, FlaskController, ElementsGenerator, Element
      Spells/                 Book, Table, TableController, Combination, Chain + конфиги заклинаний
      Battle/                 BattleController, UnitRuntime, UnitConfig, Health, TargetController
      Utility/                Pool, IAction, UIService
        Services/Localization/  свой слой локализации, LocKeys, LocalizedText
        Services/Saves/         ISaveService, PlayerProfile, ProfileSerializer/Migration,
                                фасад Saves, носители PlayerPrefs и PluginYG
  Art/                        сторонний арт, который читает Unity (102 МБ, 5 бесплатных паков)
  Plugins/UniTask/            UniTask 2.5.10 (вендорится в репо, свои asmdef)
  Plagins/DOTween/            DOTween (папка названа с опечаткой — так в проекте)
  PluginYourGames/            PluginYG 2 (v2.0092) — SDK Яндекс.Игр
  Layer Lab/                  GUI Pro-FantasyRPG — покупной UI-кит
  Resources/                  Battle, Elements, Spells, UI, Localization + DOTweenSettings
  WebGLTemplates/YandexGames/ WebGL-шаблон под Яндекс.Игры
RawArt/                       ВНЕ Assets/ намеренно: векторные исходники (AI/EPS, 377 МБ)
                              и licenses/ — реестр прав на все паки. Unity их не импортирует
```

Игровой код (кроме UniTask) живёт в `Assembly-CSharp` — **своих asmdef у `Assets/Scripts` нет**.

## Архитектура

Основная идея: **MonoBehaviour только для View и точки входа**, вся логика — в обычных C#-классах,
связанных через события (`event Action<...>`) и передачу `IUIService` в конструктор.

```
GameManager (MonoBehaviour, Start)
  └─ HomeController(IUIService, Book, BattleConfig[])
       └─ StepsController(IUIService, Book, HomeController, BattleConfig)   // одна «партия»
            ├─ FlaskController   — пазл с колбами, шлёт OnFlaskFull(Element)
            ├─ TableController   — копит собранные Element'ы, по кнопке ищет
            │                      комбинацию в Table → OnSuccessfulMerge(BaseSpell)
            └─ BattleController  — волны врагов, юниты, HP героя;
                                   spell.ApplySpell(battleController) применяет эффект
```

Ключевые связи:
- `FlaskController.OnFlaskFull` → `TableController` копит элементы.
- `TableController.OnSuccessfulMerge` → `BattleController` (заклинание само себя применяет
  через `BaseSpell.ApplySpell(BattleController)`).
- `BattleController.OnAllEnemyDie` / `OnHeroDie` → `StepsController` показывает окно победы/поражения.
- Волны берутся из `BattleConfig.Waves`; при смерти последнего врага волна инкрементится.

### Часы мира (пошаговость)

Время в игре двигает **только игрок**: перелив и варка дают такт. Такт раздаёт
`WorldClock.Tick()` в фиксированном порядке фаз: остывание котла → длящиеся эффекты
игрока → враги → союзники.

⛔ **Новая система, живущая во времени, реализует `ITickable` и регистрируется
в `WorldClock`.** Не подписывайся на `MoveCommand` напрямую (порядок фаз станет
случайным) и не пиши `UniTask.Delay` для игровых длительностей — это возврат
real-time, то есть бесплатный ресурс тому, кто просто перестал ходить.
В `Assets/Scripts/Runtime` не осталось ни одного `UniTask.Delay`, и так и должно быть.

### Сохранения

`Utility.Services.Saves` — фасад `Saves` над `ISaveService`. Формат сейва один
на все платформы (JSON через `JsonUtility`), платформенный — только носитель
(`ISaveStorage`). Правила, которые нельзя нарушать при правке `PlayerProfile`:
поля **публичные, не свойства** (у авто-свойства backing-поле зовётся
`<Version>k__BackingField`, и это имя уедет в сейвы игроков навсегда), у `Version`
**нет инициализатора** (`JsonUtility.FromJson` применяет инициализаторы, и чужой json
прочитался бы как валидный профиль), словарей и полиморфизма в профиле не бывает.
Разбор — [docs/04, решение 8](docs/04-architecture.md).

### UIService

`IUIService` (`Utility/Services/UIService`) — реестр окон: `Show<T>()`, `Hide<T>()`, `Get<T>()`, `HideAll()`.
Реализация `UIService` — синглтон-MonoBehaviour, собирающий все `UIWindow` на сцене
через `FindObjectsByType<UIWindow>(FindObjectsInactive.Exclude)` в `InitWindows()`.
Окна наследуются от `UIWindow` / `UISimpleWindow` / `UISimpleClosableWindow` / `UISimpleQuestionWindow`.

**Важно:** окна ищутся только среди активных объектов, поэтому все `UIWindow` должны быть
активны на сцене в момент `Awake`, а `GameManager.Start` уже вызывает `_uiService.HideAll()`.

### Локализация

`Utility.Services.Localization` — свой слой, **не привязанный к PluginYG**.
Игровой код зовёт `Localization.Get(key)`; строки лежат в `LocalizationTable`
(ScriptableObject в `Resources/Localization/`), русский — язык-источник и фолбэк,
английский заведён пустым заделом под фазу 2.

Откуда берётся язык — цепочка `ILanguageSource`: выбор игрока → площадка → система →
русский. Площадка изолирована в единственном файле `PluginYGLanguageSource.cs`
за `#if PLUGIN_YG_2 && Localization_yg` (двойной guard намеренно: дефайн `PLUGIN_YG_2`
стоит, а модуль локализации PluginYG **не установлен**).

Тексты на сцене ставит компонент `LocalizedText`. Момент установки — два:
собственный `OnEnable` (объекты из пула) и `UIWindow.Show() → ApplyLocalization()`
(окна гасятся `Canvas.enabled`, поэтому `OnEnable` при показе окна не срабатывает).

### Освобождение ресурсов

Есть интерфейс `IAction` с `ClearAction()` — контроллеры собирают подписчиков в `List<IAction>`
и чистят их в `Exit()`. Контроллеры также занулают свои `event`'ы в `Exit()`.
При добавлении новых подписок не забывай про соответствующий `Exit()`/`ClearAction()`.

### Асинхронность

Используется **UniTask** (`Cysharp.Threading.Tasks`), не `System.Threading.Tasks`.
Атаки/кулдауны юнитов крутятся в `TargetController` через UniTask.

## Соглашения по коду

- ⛔ **Ни одной пользовательской строки литералом — только ключ локализации.**
  Ни в коде, ни в ассете, ни в TMP на сцене или в префабе. Строка в коде —
  `Localization.Get(LocKeys.Xxx)`; в сцене/префабе — компонент `LocalizedText`
  (`Key` — из таблицы, `Code` — ставит код, `Placeholder` — заглушка чужого кита);
  в ассете — поле-ключ (`_nameKey`, `_nameHeroKey`, `_classHeroKey`).
  Таблица строк — `Assets/Resources/Localization/Localization Table.asset`
  (русский заполняем, английский — задел фазы 2, пустой).
  Проверка — `Tools → Локализация → Проверить сцену и ассеты` (Ctrl+Shift+L),
  отчёт обязан быть без ошибок. Рецепт — [docs/05](docs/05-extending.md#добавить-строку).
  Нарушение ничего не ломает и не видно в диффе — поэтому проверку прогоняй руками.
- Приватные поля — `_camelCase`, `[SerializeField] private` для инспекторных ссылок.
- Namespace'ы: `Core.Battle`, `Core.Flask`, `Core.Spells`, `Core.Steps`, `Meta`, `Utility.Services.UI`.
  У `UIService` namespace'а нет (глобальный) — так исторически.
- Конфиги — `ScriptableObject` (`BattleConfig`, `UnitConfig`, `Book`, `*SpellConfig`), лежат в `Resources/`.
- **Файлы с кириллицей сохраняй в UTF-8 (желательно с BOM).** Часть файлов раньше была в CP1251,
  из-за чего русские строки превращались в мусор при сборке — исправлено, но VS может
  сохранить обратно в ANSI, если BOM потеряется.
- ⛔ **Арт из `Assets/Art/` и `RawArt/` нельзя скармливать генеративным моделям.**
  Лицензия CraftPix прямо запрещает использовать их ассеты для обучения, дообучения,
  тестирования и улучшения AI/ML-систем — это 4 пака из 5 (исключение — Kenney, CC0).
  Ни «дорисуй в том же стиле», ни апскейл нейросетью. Реестр прав —
  [`RawArt/licenses/README.md`](RawArt/licenses/README.md).

## История апгрейда 2022 → Unity 6.5

Что уже починено:

1. `Assets/Plugins/UniTask/Editor/UniTaskTrackerTreeView.cs` — в Unity 6.5 негенериковые
   `TreeView` / `TreeViewItem` / `TreeViewState` помечены `[Obsolete(error: true)]`,
   что валило сборку `UniTask.Editor` (CS0619, подавить `#pragma` нельзя — это error, не warning).
   Переведено на `TreeView<int>` / `TreeViewItem<int>` / `TreeViewState<int>`.
   **Это локальная правка вендоренного UniTask — при обновлении UniTask её нужно наложить заново.**
2. `UIService.cs` — `FindObjectsOfType<T>()` → `FindObjectsByType<T>(FindObjectsInactive.Exclude)`.
3. `UIFlask.cs`, `UIFlaskWindow.cs`, `TableController.cs` — перекодированы из CP1251 в UTF-8.

4. `ProjectSettings.asset` — возвращены сдвинутые апгрейдом значения:
   Color Space → Linear, WebGL Exception Support → Explicitly Thrown Only,
   Managed Stripping (WebGL) → Low.

Пакетные изменения от апгрейда (ожидаемые, трогать не надо):
- `com.unity.textmeshpro` удалён — TMP теперь внутри `com.unity.ugui` 2.5.0.
- `com.unity.modules.vr` удалён, добавлены `accessibility`, `adaptiveperformance`,
  `physicscore2d`, `vectorgraphics`, `com.unity.multiplayer.center`.

Добавлено отдельно (не апгрейдом):
- `com.unity.ai.assistant` — нужен, чтобы работал официальный Unity MCP.
- `com.unity.pipeline` — сервер для Unity CLI (`unity command …`).

Остаточные warning'и в `Assets/PluginYourGames/Example/` (устаревший `FindObjectsSortMode`) —
это демо-сцены вендора, папку `Example` можно удалить целиком.

Подробности по всему апгрейду и по инструментам — в [docs/08-tooling.md](docs/08-tooling.md).
