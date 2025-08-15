using Core.Spells;
using System;
using TMPro;
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
        [SerializeField] private TextMeshProUGUI _heroNameText;
        [SerializeField] private TextMeshProUGUI _classNameText;
        [SerializeField] private Image _iconClassImage;
        [SerializeField] private Image _heroIconImage;

        public void Init(bool value)
        {
            _heroToggle.isOn = value;
            _heroToggle.onValueChanged.AddListener((value) => OnHeroSelected?.Invoke(value));
            _heroNameText.text = _heroBook.NameHero;
            _classNameText.text = _heroBook.ClassHero;
            _iconClassImage.sprite = _heroBook.IconClass;
            _heroIconImage.sprite = _heroBook.HeroIcon;
        }

        public void ClearAction()
        {
            _heroToggle.onValueChanged.RemoveAllListeners();
            OnHeroSelected = null;
        }
    }

}