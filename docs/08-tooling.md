# 08 — Инструменты и сборка

*Срез: 1 августа 2026.*

## Окружение

- **Unity 6000.5.3f1** (Unity 6.5). Установлен в `C:\Program Files\Unity\Hub\Editor\6000.5.3f1`.
- Целевая платформа: **WebGL**, шаблон `PROJECT:YandexGames` (`Assets/WebGLTemplates/YandexGames`).
- Единственная сцена в билде: `Assets/Scenes/SampleScene.unity`.
- Дефайны для WebGL: `DOTWEEN;PLUGIN_YG_2;TMP_YG2;RU_YG2;YandexGamesPlatform_yg`.

---

## Апгрейд с Unity 2022 на 6.5

Проект был поднят с 2022 в июле 2026. Что пришлось починить:

### 1. UniTask не собирался (блокировало весь проект)

В Unity 6.5 негенериковые `TreeView` / `TreeViewItem` / `TreeViewState` из
`UnityEditor.IMGUI.Controls` помечены `[Obsolete(error: true)]`. Вендоренный
UniTask 2.5.10 их использует, из-за чего сборка `UniTask.Editor` падала
с `CS0619`, а Unity блокировал Play Mode целиком.

Подавить `#pragma warning disable` нельзя — `CS0619` это **ошибка**, а не предупреждение.

**Правка:** [`UniTaskTrackerTreeView.cs`](../Assets/Plugins/UniTask/Editor/UniTaskTrackerTreeView.cs)
переведён на `TreeView<int>` / `TreeViewItem<int>` / `TreeViewState<int>`.

> ⚠️ Это локальная правка вендоренного пакета. **При обновлении UniTask её нужно
> накатить заново.**

### 2. Устаревший API поиска объектов

`UIService.cs`: `FindObjectsOfType<T>()` → `FindObjectsByType<T>(FindObjectsInactive.Exclude)`.
Семантика «только активные» сохранена намеренно — от неё зависит сбор окон.

### 3. Кодировки

`UIFlask.cs`, `UIFlaskWindow.cs`, `TableController.cs` были сохранены в **CP1251**.
Roslyn читает исходники как UTF-8, поэтому русская строка в `TableController.cs`
(«Ничего не получилось((») превращалась в мусор прямо в игре.

Перекодированы в UTF-8 с BOM. **Следи, чтобы Visual Studio не сохранила обратно в ANSI.**

### 4. Настройки проекта

Апгрейд сдвинул три значения, они возвращены обратно:

| Настройка | Стало после апгрейда | Возвращено |
|---|---|---|
| Color Space | Gamma | **Linear** |
| WebGL Exception Support | Full With Stacktrace | **Explicitly Thrown Only** |
| Managed Stripping (WebGL) | Minimal | **Low** |

Последние две напрямую влияют на размер и скорость загрузки WebGL-билда,
что критично для Яндекс.Игр.

### 5. Пакеты (штатная миграция, трогать не надо)

- `com.unity.textmeshpro` **удалён** — TMP теперь внутри `com.unity.ugui` 2.5.0.
- `com.unity.modules.vr` удалён.
- Добавлены `accessibility`, `adaptiveperformance`, `physicscore2d`,
  `vectorgraphics`, `com.unity.multiplayer.center`.

---

## Unity CLI

Установлен `unity` **1.0.0-beta.3** в `%LOCALAPPDATA%\Unity\bin\unity.exe`.

```powershell
$env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex
```

Ставится в user-scope, админка не нужна, PATH обновляется — но **только для новых
терминалов**.

В проект добавлен пакет `com.unity.pipeline@0.4.0-exp.1`, который поднимает в редакторе
сервер (порт виден в `unity status`) и даёт ~150 команд.

### Что реально полезно

```bash
unity status                      # какие редакторы подключены, порт, состояние
unity command recompile           # компиляция БЕЗ переключения в редактор
unity command recompile_status    # {"status":"completed","failed":false,"errors":[]}
unity command console             # консоль Unity в терминал
unity command get_console_logs    # структурированные логи
unity command set_autotick        # редактор тикает без фокуса
unity command capture_game_view   # скриншот Game View
unity shell                       # прогретый процесс, если гонять команды пачками
```

Это снимает главную боль разработки: **не нужно переключаться в Unity, чтобы
он перекомпилировал код**.

### Синтаксис аргументов — важно

Аргументы команд передаются как `--ключ значение` **после** имени команды.
Формы `--args '{"json":...}'`, `key=value` и позиционные значения **не работают**
(тихо игнорируются или дают 400).

```bash
# правильно
unity command eval_file --project-path <проект> --file C:\path\to\probe.cs
unity command read_text_file --path Assets/Scripts/Runtime/GameManager.cs

# НЕ работает
unity command eval_file --args '{"file":"..."}'
unity command eval_file "file=C:\path"
```

### Сборка

**`unity build` не подходит** при открытом редакторе: он поднимает второй экземпляр
Unity в batch mode и упирается в блокировку проекта. Плюс требует `--execute-method`
со своим статическим C#-методом, которого в проекте нет.

Используй пайплайн-команду — она отдаёт задание уже открытому редактору:

```bash
unity command build --dry_run true     # валидация без сборки
unity command build                    # асинхронно, затем:
unity command build_status             # полный BuildReport
```

Проверено: WebGL-модуль установлен, активный таргет уже `WebGL`, сцена в списке.

### Тесты

`unity test` работает, но **тестов в проекте 0** (`unity command list_tests` → `Count: 0`).
Пока их не напишут, команда бесполезна.

---

## MCP

Официальный Unity MCP работает через `~/.claude.json` → `unity-mcp` →
`.unity/relay/relay_win.exe --mcp`.

**Важно понимать:** конфиг MCP **глобальный**, а мост создаёт **пакет внутри проекта**.
Relay ищет дескриптор в `~/.unity/mcp/connections/bridge-*.json`, а появляется тот
только если в проекте стоит **`com.unity.ai.assistant`**.

Именно поэтому MCP «то есть, то нет» при переключении между проектами — конфиг
исправен, не хватает пакета. В CatZombii пакет добавлен (`2.14.0-pre.1`).
Мост поднимается примерно через 5 минут после того, как редактор получит фокус.

Отдельно от этого CLI имеет собственный `unity mcp` — параллельная система, не замена.
`unity mcp configure claude-code` делегирует в `claude mcp add`, так что требует
`claude` в PATH.

Известная безобидная ошибка в консоли после установки AI-пакета:
`FSBTool ERROR ... FailedDownload.wav ... too short` — дефект внутри вендорского
пакета, на проект не влияет.

---

## Проверка компиляции без редактора

Если Unity закрыт или занят, можно собрать сгенерированные им `.csproj` напрямую
Roslyn'ом из .NET SDK:

```bash
CSC="/c/Program Files/dotnet/sdk/9.0.304/Roslyn/bincore/csc.dll"
dotnet "$CSC" "@<name>.rsp"
```

Response-файл собирается из `.csproj`: `<DefineConstants>`, `<Compile Include>`,
`<HintPath>`, `<ProjectReference>`. Порядок сборки:

```
UniTask → UniTask.{Linq,Addressables,DOTween,TextMeshPro,Editor}
        → Assembly-CSharp → Assembly-CSharp-Editor
```

Это точная проверка на ошибки компиляции, но она **не проверяет сцены, ассеты
и рантайм** — для этого нужен редактор.

---

## Прочее

- Логи редактора: `Logs/Editor.log`, ошибки компиляции ищутся по `error CS`.
- Папка `Assets/Plagins/` (DOTween) названа с опечаткой — так в проекте, не переименовывай
  без нужды, сломаются `.meta`-ссылки.
- Папка `Assets/Resources/UI/Core/Pfrefabs/` — тоже опечатка, аналогично.
- `Assets/PluginYourGames/Example/` — демо-сцены вендора, дают предупреждения
  `CS0618`. Папку можно удалить целиком.
