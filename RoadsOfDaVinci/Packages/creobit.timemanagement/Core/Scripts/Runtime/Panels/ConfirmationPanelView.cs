using System;
using Creobit.UI;
using UnityEngine;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.UI;
using TMPro;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Panels
{
    public class ConfirmationPanelView : PanelData
    {
        [SerializeField] private TMP_Text _titleText;

        [SerializeField] private TMP_Text _descriptionText;

        [SerializeField] private Button _yesButton;
        [SerializeField] private Button _noButton;

        private Action _yes;
        private Action _no;

        public override UniTask Load()
        {
            Observable
                .EveryValueChanged(gameObject, x => x.activeSelf) // detect if panel open or close
                .Where(isOpened => !isOpened) // detect only close panel
                .Skip(1) // skip initialization
                .Subscribe((_) => Reset())
                .AddTo(this);

            _yesButton.onClick.AddListener(OnYesButtonClicked);
            _noButton.onClick.AddListener(OnNoButtonClicked);

            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            _yesButton.onClick.RemoveListener(OnYesButtonClicked);
            _noButton.onClick.RemoveListener(OnNoButtonClicked);
        }

        public void SetInputData(string title, string description, Action yes, Action no)
        {
            _titleText.text = title;
            _descriptionText.text = description;

            _yes = yes;
            _no = no;
        }

        private void Reset()
        {
            _yes = null;
            _no = null;
        }

        private void OnYesButtonClicked() => _yes?.Invoke();

        private void OnNoButtonClicked() => _no?.Invoke();
    }
}