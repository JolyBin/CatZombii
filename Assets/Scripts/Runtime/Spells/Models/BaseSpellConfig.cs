using Core.Battle;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using Utility.Services.Localization;

namespace Core.Spells
{
    public abstract class BaseSpellConfig: ScriptableObject
    {
        [Tooltip("КЛЮЧ строки, а не сама строка. Соглашение: spell.<имя_заклинания>. " +
                 "Текст живёт в Resources/Localization/Localization Table — рецепт в docs/05.")]
        [SerializeField] private string _nameKey;

        /// <summary>Ключ имени — нужен редакторной проверке и миграциям, не игре.</summary>
        public string NameKey => _nameKey;

        /// <summary>
        /// Короткое имя заклинания — то, что печатается игроку в
        /// <c>UITableWindow.ShowResult</c> и в карточке комбинации.
        /// Разрешается ПО КЛЮЧУ при каждом обращении, а не кэшируется: смена языка
        /// не должна требовать пересоздания ассетов, а зовётся это раз в варку.
        /// </summary>
        public string Name => Localization.Get(_nameKey);

        /// <summary>
        /// Иконка для предпросмотра на кнопке варки: docs/10 §4 требует «иконка + число»,
        /// а не прозу. Пока арта нет, поле пустое — кнопка оставляет иконку по умолчанию.
        /// </summary>
        [field: SerializeField] public Sprite Icon { get; private set; }

        /// <summary>
        /// Число для предпросмотра: урон одной цели, урон по каждому в AoE, суммарное лечение.
        /// Берётся из тех же полей, из которых собирается сам эффект, — второго источника
        /// чисел рядом с балансом не заводим, иначе кнопка начнёт врать после правки ассета.
        /// </summary>
        public virtual int PreviewValue => 0;

        /// <summary>
        /// ЕСТЬ ЛИ У ЭТОГО РЕЦЕПТА ЭФФЕКТ, ИЛИ ЭТО ЕЩЁ ЗАГОТОВКА.
        ///
        /// Существует ровно из-за <see cref="Spell"/> — класса-заглушки, у которого
        /// <see cref="GetSpell"/> бросает <c>NotImplementedException</c> (docs/06 §9).
        /// Восемь заклинаний Мага и Чаровницы висят на нём, и для игрока это неотличимо
        /// от поломки: выбрал героя, сварил рецепт — исключение посреди боя.
        ///
        /// ⚠️ ПРОВЕРЯТЬ НАДО ДО ВЫЗОВА, А НЕ ЛОВИТЬ ИСКЛЮЧЕНИЕ. Свойство именно затем
        /// и заведено: <c>try/catch</c> вокруг <see cref="GetSpell"/> означал бы «узнаём
        /// о незаконченном контенте в тот момент, когда игрок уже потратил на него бой».
        ///
        /// Значение по умолчанию — <c>true</c>, и это важнее, чем кажется: новый класс
        /// эффекта считается рабочим, ничего не переопределяя. Наоборот было бы ловушкой —
        /// геймдизайнер дописал бы эффект, а герой остался бы запертым без единой подсказки.
        /// Поэтому «дописал — открылось» стоит ноль правок кода и ноль правок ассетов:
        /// достаточно переставить ассет заклинания с <see cref="Spell"/> на настоящий конфиг.
        /// </summary>
        public virtual bool IsImplemented => true;

        public abstract BaseSpell GetSpell();
    }

    public abstract class BaseSpell
    {
        /// <summary>
        /// Применяет заклинание. Токен живёт столько же, сколько бой:
        /// отложенные эффекты обязаны прерываться по нему.
        /// </summary>
        public virtual UniTask ApplySpell(BattleController battleController, CancellationToken token) => UniTask.CompletedTask;
    }
}
