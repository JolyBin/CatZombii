# 05 — Как расширять

Практические рецепты. Все пути — от корня проекта.

---

## Добавить заклинание

Самая частая задача. Шаблон отработан на Warrior'е — восемь заклинаний
Wizard/Enchantress пишутся ровно по нему.

### 1. Код

Создай файл в `Assets/Scripts/Runtime/Spells/Models/<Герой>/`.
Один файл содержит **и конфиг, и рантайм-класс** — так принято в проекте.

```csharp
using Core.Battle;
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

        public override async void ApplySpell(BattleController battleController)
        {
            if (battleController.EnemySquad.Length == 0)
                return;                       // ← ОБЯЗАТЕЛЬНО: иначе NRE
            battleController.HeroTarget.Health.TakeDamage(_damage);
        }
    }
}
```

**Обязательно проверяй `EnemySquad.Length == 0` перед обращением к `HeroTarget`** —
иначе `NullReferenceException`, когда волна уже зачищена, а заклинание долетело.

### 2. Ассет

`Create → Spells → <Герой> → ...` — положи в
`Assets/Resources/Spells/Books/<Герой> Book/`. Заполни `Name` (текст для игрока)
и параметры.

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
| `AddFriend(UnitConfig)` | Заспавнить союзника (⚠️ максимум 3 из-за сцены) |

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
2. Заполни `NameHero`, `ClassHero`, `HP`, `IconClass`, `HeroIcon`.
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
3. **Конфиг**: `Create → Battle Configs → Create Unit Config`.

| Поле | Смысл |
|---|---|
| `HP` | Здоровье |
| `AttackCooldown` | Мс между атаками. **`0` = юнит не атакует** |
| `TargetPriority` | Кого враги бьют охотнее. Больше = приоритетнее. Герой = 0 |
| `AttackConfig` | Ссылка на конфиг атаки |
| `UnitPrefab` | Ссылка на префаб с `UIUnit` |

> ⚠️ **`AttackCooldown = 0` сейчас роняет игру** при смерти такого юнита —
> [баг №3](06-known-issues.md#3--unitruntimedispose--мина). Сначала почини,
> потом делай пассивных юнитов.

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

---

## Правила, которые легко нарушить

1. **Файлы с кириллицей сохраняй в UTF-8 (лучше с BOM).** Три файла проекта раньше
   были в CP1251, и русские строки превращались в мусор при сборке. Исправлено,
   но Visual Studio может сохранить обратно в ANSI, если BOM потеряется.
2. **Не меняй `Element.ID` существующих элементов** — это ключи trie, сломаются
   все книги разом.
3. **Каждой подписке — свой `ClearAction()`/`Exit()`.** Иначе утечка между боями.
4. **Проверяй пустой `EnemySquad`** в заклинаниях.
5. **Лимиты состава есть и в коде, и в сцене** — меняешь один, проверь второй.
6. Правка `Assets/Plugins/UniTask/Editor/UniTaskTrackerTreeView.cs` — локальная
   (совместимость с Unity 6.5). При обновлении UniTask её надо накатить заново,
   см. [08 — Инструменты](08-tooling.md).
