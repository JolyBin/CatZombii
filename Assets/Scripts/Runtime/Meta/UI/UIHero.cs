using Core.Spells;
using System;
using UnityEngine;
using UnityEngine.UI;


namespace Meta.UI
{
    public class UIHero : MonoBehaviour, IAction
    {
        public event Action<bool> OnHeroSelected;

        public Book HeroBook => _heroBook;

        [SerializeField] private Book _heroBook;
        [SerializeField] private Toggle _heroToggle;

        public void Init(bool value)
        {
            _heroToggle.isOn = value;
            _heroToggle.onValueChanged.AddListener((value) => OnHeroSelected?.Invoke(value));
        }

        public void ClearAction()
        {
            _heroToggle.onValueChanged.RemoveAllListeners();
            OnHeroSelected = null;
        }
    }

}