using Cysharp.Threading.Tasks;
using Flexalon;
using System;
using System.Threading;
using Creobit.Localization;
using TMPro;
using UltEvents;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip
{
    public class CantReachAnotherObjectTooltipView : MonoBehaviour, ISecondaryTooltipView
    {
        [SerializeField]
        private FlexalonObject _rootFlexalon;

        [SerializeField]
        private TMP_Text _tooltipText;

        [field: SerializeField]
        public UltEvent OnShow { get; set; }

        [field: SerializeField]
        public float Duration { get; set; }

        [field: SerializeField]
        public UltEvent OnHide { get; set; }

        public CancellationTokenSource HideCancellation { get; private set; } = new CancellationTokenSource();

        public Vector2 Size
        {
            get
            {
                var rect = (_rootFlexalon.transform as RectTransform).rect;

                return new(rect.width, rect.height);
            }
        }

        public GameObject CurrentGameObject => gameObject;

        public void Show()
        {
            OnShow.InvokeSafe();
            gameObject.SetActive(true);
            DelayHide().Forget();
        }

        private async UniTask DelayHide()
        {
            if (HideCancellation.Token.CanBeCanceled) HideCancellation?.Cancel();
            try
            {
                HideCancellation = new CancellationTokenSource();
                await UniTask.WaitForSeconds(Duration, cancellationToken: HideCancellation.Token);
                ForceHide();
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        public void ForceHide()
        {
            OnHide.InvokeSafe();
            gameObject.SetActive(false);
        }

        public void SetPosition(Vector2 position)
        {
            transform.localPosition = position;
        }

        public void SetTooltipText(string primaryText, string secondaryText = null)
        {
            _tooltipText.text = LocalizationService.Instance.GetText(primaryText);
        }

        public void ForceUpdate()
        {
            _rootFlexalon.ForceUpdate();
        }
    }
}