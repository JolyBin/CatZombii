using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Utility.Services.UI
{
    public abstract class UISimpleQuestionWindow : UISimpleClosableWindow
    {
        [SerializeField] private TextMeshProUGUI confirmDelayCounter;
        [SerializeField] private Button confirmButton;

        public Action ConfirmClickEvent;

        public override void Show()
        {
            base.Show();
            confirmButton.onClick.AddListener(ConfirmClickHandler);
        }

        public override void Hide(Action onHide = null)
        {
            ConfirmClickEvent = null;
            base.Hide(onHide);
            confirmButton.onClick.RemoveListener(ConfirmClickHandler);
            StopAllCoroutines();
        }

        protected void SetConfirmDelay(float delaySeconds)
        {
            StartCoroutine(ConfirmDelayCoroutine(delaySeconds));
        }

        private IEnumerator ConfirmDelayCoroutine(float delaySeconds)
        {
            confirmButton.interactable = false;
            var waiter = new WaitForSeconds(1);

            while (delaySeconds > 0)
            {
                if (confirmDelayCounter)
                    confirmDelayCounter.text = delaySeconds.ToString("0");
                
                yield return waiter;
                delaySeconds--;
            }

            if (confirmDelayCounter)
                confirmDelayCounter.text = string.Empty;
            
            confirmButton.interactable = true;
        }

        private void ConfirmClickHandler()
        {
            ConfirmClickEvent?.Invoke();
        }
    }
}