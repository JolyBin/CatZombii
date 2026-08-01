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
| **Принятый дизайн игры** (ядро, прогрессия, экономика) | **[docs/10-progression-design.md](docs/10-progression-design.md)** |

**Быстрые факты, которые чаще всего нужны:** проект компилируется без ошибок;
Wizard и Enchantress — заглушки, бросают `NotImplementedException`; уровни 4–7
собраны, но не подключены к сцене; сохранений и интеграции с Яндекс.Играми нет.

**Важно про статус документации:** 01.08.2026 владелец снял ограничение скоупа, и ядро
игры решено переработать — **элементы в котле стынут** (каждый живёт 15–20 с и выпадает
из хвоста), поэтому «забрать эффект сейчас или достроить сильный» становится настоящей
ставкой. Документы 02 и 03 описывают **прототип как он есть**, документ 10 — **принятый
дизайн**. При расхождении прав документ 10.

⚠️ **Вёрстка контента заблокирована** до замера такта схлопывания (шаг 0 в docs/10 §8):
из этой константы выведены длительности волн, HP, награды и вся экономика.

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
      Steps/                  StepsController — склейка боя, колб и стола; окна боя/победы/поражения
      Flask/                  пазл-механика колб: Flask, FlaskController, ElementsGenerator, Element
      Spells/                 Book, Table, TableController, Combination, Chain + конфиги заклинаний
      Battle/                 BattleController, UnitRuntime, UnitConfig, Health, TargetController
      Utility/                Pool, IAction, UIService
  Plugins/UniTask/            UniTask 2.5.10 (вендорится в репо, свои asmdef)
  Plagins/DOTween/            DOTween (папка названа с опечаткой — так в проекте)
  PluginYourGames/            PluginYG 2 (v2.0092) — SDK Яндекс.Игр
  Layer Lab/                  GUI Pro-FantasyRPG — покупной UI-кит
  Resources/                  Battle, Elements, Spells, UI + DOTweenSettings
  WebGLTemplates/YandexGames/ WebGL-шаблон под Яндекс.Игры
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

### UIService

`IUIService` (`Utility/Services/UI`) — реестр окон: `Show<T>()`, `Hide<T>()`, `Get<T>()`, `HideAll()`.
Реализация `UIService` — синглтон-MonoBehaviour, собирающий все `UIWindow` на сцене
через `FindObjectsByType<UIWindow>(FindObjectsInactive.Exclude)` в `InitWindows()`.
Окна наследуются от `UIWindow` / `UISimpleWindow` / `UISimpleClosableWindow` / `UISimpleQuestionWindow`.

**Важно:** окна ищутся только среди активных объектов, поэтому все `UIWindow` должны быть
активны на сцене в момент `Awake`, а `GameManager.Start` уже вызывает `_uiService.HideAll()`.

### Освобождение ресурсов

Есть интерфейс `IAction` с `ClearAction()` — контроллеры собирают подписчиков в `List<IAction>`
и чистят их в `Exit()`. Контроллеры также занулают свои `event`'ы в `Exit()`.
При добавлении новых подписок не забывай про соответствующий `Exit()`/`ClearAction()`.

### Асинхронность

Используется **UniTask** (`Cysharp.Threading.Tasks`), не `System.Threading.Tasks`.
Атаки/кулдауны юнитов крутятся в `TargetController` через UniTask.

## Соглашения по коду

- Приватные поля — `_camelCase`, `[SerializeField] private` для инспекторных ссылок.
- Namespace'ы: `Core.Battle`, `Core.Flask`, `Core.Spells`, `Core.Steps`, `Meta`, `Utility.Services.UI`.
  У `UIService` namespace'а нет (глобальный) — так исторически.
- Конфиги — `ScriptableObject` (`BattleConfig`, `UnitConfig`, `Book`, `*SpellConfig`), лежат в `Resources/`.
- **Файлы с кириллицей сохраняй в UTF-8 (желательно с BOM).** Часть файлов раньше была в CP1251,
  из-за чего русские строки превращались в мусор при сборке — исправлено, но VS может
  сохранить обратно в ANSI, если BOM потеряется.

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
