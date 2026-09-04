using System;
using Creobit.UI;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Panels
{
    public class AlertPanelView : PanelData
    {
        [SerializeField] private TMP_Text _titleText;

        [SerializeField] private TMP_Text _descriptionText;

        [SerializeField] private Button _okButton;

        private Action _ok;


        public override UniTask Load()
        {
            Observable
                .EveryValueChanged(gameObject, x => x.activeSelf) // detect if panel open or close
                .Where(isOpened => !isOpened) // detect only close panel
                .Skip(1) // skip initialization
                .Subscribe((_) => Reset())
                .AddTo(this);

            _okButton.onClick.AddListener(OnOkButtonClicked);

            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            _okButton.onClick.RemoveListener(OnOkButtonClicked);
        }

        public void SetInputData(string title, string description, Action ok)
        {
            _titleText.text = title;
            _descriptionText.text = description;
            ;
            _ok = ok;
        }

        private void Reset()
        {
            _ok = null;
        }

        private void OnOkButtonClicked() => _ok?.Invoke();
    }
}