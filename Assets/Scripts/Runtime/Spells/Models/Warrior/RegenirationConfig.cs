using Core.Battle;
using Cysharp.Threading.Tasks;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Regeniration Config", menuName = "Spells/Warrior/Create Regeniration Config")]
    public class RegenirationConfig : BaseSpellConfig
    {
        [SerializeField] private int _healthvalue = 10;
        [SerializeField] private int _timer = 2000;
        [SerializeField] private int _count = 3;
        // суммарное лечение: игроку важен итог, а не размер одного тика
        public override int PreviewValue => _healthvalue * _count;
        public override BaseSpell GetSpell() => new Regeniration(_healthvalue, _timer, _count);
    }

    public class Regeniration : BaseSpell
    {
        private int _healthvalue;
        private int _timer;
        private int _count;

        public Regeniration(int healthvalue, int timer, int count)
        {
            _healthvalue = healthvalue;
            _timer = timer;
            _count = count;
        }

        public override async UniTask ApplySpell(BattleController battleController, CancellationToken token)
        {
            if (battleController.HeroHealth.CurrentHP == 0)
                return;
            int count = 0;
            while (count < _count)
            {
                if (token.IsCancellationRequested || battleController.HeroHealth.CurrentHP == 0)
                    return;
                battleController.HeroHealth.Heal(_healthvalue);
                count++;

                bool isCanceled = await UniTask.Delay(_timer, cancellationToken: token).SuppressCancellationThrow();
                if (isCanceled)
                    return;
            }
        }
    }
}

