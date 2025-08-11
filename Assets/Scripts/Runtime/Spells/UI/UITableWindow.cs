using Core.Flask.UI;
using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.UI;

namespace Core.Spells.UI
{
    public class UITableWindow : UIWindow
    {
        public event Action OnClickCheckCombinationButton;

        private const float _animDuration = 0.5f;

        [SerializeField] private UIFullFlask[] _flasks;
        [SerializeField] private Button _checkCombinationButton;
        [SerializeField] private TextMeshProUGUI _resultMergeText;

        private Sequence _sequence;
        private int _curretnEmptyPositions;


        public override void Show()
        {
            _checkCombinationButton.onClick.AddListener(()  => OnClickCheckCombinationButton?.Invoke());
            ResetPositions();
            base.Show();
        }

        public override void Hide(Action onHide = null)
        {
            _checkCombinationButton.onClick.RemoveAllListeners();
            base.Hide(onHide);
        }

        public void ClearFlasks()
        {
            _sequence = DOTween.Sequence();
            foreach(UIFullFlask flask in _flasks)
            {
                _sequence
                    .Join(flask.transform.DOScale(0, _animDuration))
                    .Join(flask.transform.DORotate(new Vector3(0, 0, 360), _animDuration, RotateMode.FastBeyond360));
            }
            _sequence.OnComplete(() => ResetPositions());
        }

        public void ShowFullFlask(Sprite sprte)
        {
            UIFullFlask newFlask = _flasks[_curretnEmptyPositions];
            _curretnEmptyPositions++;

            newFlask.gameObject.SetActive(true);
            newFlask.SetSprite(sprte);

            newFlask.transform.localScale = Vector3.zero;

            _sequence = DOTween.Sequence();
            _sequence
                .Join(newFlask.transform.DOScale(1, _animDuration))
                .Join(newFlask.transform.DORotate(new Vector3(0, 0, 360), _animDuration, RotateMode.FastBeyond360));

        }

        public void ShowResult(bool result, string name)
        {
            _resultMergeText.text = name;
        }

        private void ResetPositions()
        {
            foreach (UIFullFlask flask in _flasks)
            {
                flask.gameObject.SetActive(false);
            }
            _curretnEmptyPositions = 0;
        }
    }
}
