using Core.Battle;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Core.Spells
{
    public abstract class BaseSpellConfig: ScriptableObject
    {
        [field: SerializeField] public string Name { get; private set; }
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
