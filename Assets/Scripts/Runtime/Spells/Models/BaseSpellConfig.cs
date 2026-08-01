using Core.Battle;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Core.Spells
{
    public abstract class BaseSpellConfig: ScriptableObject
    {
        [field: SerializeField] public string Name { get; private set; }

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
