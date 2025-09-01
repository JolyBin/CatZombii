using Core.Battle;
using Cysharp.Threading.Tasks;
using System.Linq;
using UnityEngine;

namespace Core.Spells
{
    [CreateAssetMenu(fileName = "Regeniration Config", menuName = "Spells/Warrior/Create Regeniration Config")]
    public class RegenirationConfig : BaseSpellConfig
    {
        [SerializeField] private int _healthvalue = 10;
        [SerializeField] private int _timer = 2000;
        [SerializeField] private int _count = 3;
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

        public override async void ApplySpell(BattleController battleController)
        {
            if (battleController.HeroHealth.CurrentHP == 0)
                return;
            int count = 0;
            while (count < _count)
            {
                battleController.HeroHealth.Heal(_healthvalue);
                count++;
                await UniTask.Delay(_timer);
            }
        }
    }
}

