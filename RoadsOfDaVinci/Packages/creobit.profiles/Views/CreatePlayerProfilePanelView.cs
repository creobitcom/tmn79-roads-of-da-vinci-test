using System;
using System.Text;
using System.Threading;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.UI;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
namespace Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Views
{
    public class CreatePlayerProfilePanelView : PanelData
    {
        [SerializeField] private TMP_InputField _nameInputField;

        [SerializeField] private Button _okButton;
        [SerializeField] private Button _cancelButton;

        private PlayerProfilesRulesData _rules;
        private PlayerProfilesService _profilesService;
        private IPlayerPrefsSaveProvider _saveProvider;
        private CancellationTokenSource _source;
        private Action<string> _ok;
        private Action _cancel;

        [Inject]
        private void Construct(IPlayerPrefsSaveProvider saveController, IPlayerProfilesController profilesController)
        {
            _rules = profilesController.Rules;
            _saveProvider = saveController;
        }

        public override UniTask Load()
        {
            _source = new CancellationTokenSource();

            var panelStateSubscription = Observable
                  .EveryValueChanged(gameObject, x => x.activeSelf) // detect if panel open or close
                  .Where(isOpened => !isOpened) // detect only close panel
                  .Skip(1) // skip initialization
                  .Subscribe((_) => Reset());

            panelStateSubscription.AddTo(_source.Token);
            panelStateSubscription.AddTo(this);

            _nameInputField.characterLimit = _rules.MaxNameLength;

            _cancelButton.gameObject.SetActive(
                _saveProvider.TryGetValue(PlayerProfilesService.RuntimeConstants.PlayerProfilesPrefs.FirstProfile) != "TRUE");

            _nameInputField.onValueChanged.AddListener(OnPlayerNameChanged);
            _okButton.onClick.AddListener(OnOkButtonClicked);
            _cancelButton.onClick.AddListener(OnCancelButtonClicked);

            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            _source.Cancel();
            _okButton.onClick.RemoveListener(OnOkButtonClicked);
            _cancelButton.onClick.RemoveListener(OnCancelButtonClicked);
        }

        private void OnPlayerNameChanged(string text)
        {
            var sb = new StringBuilder(text.Length);
            
            foreach (var c in text)
            {
                if (char.IsLetter(c))
                {
                    sb.Append(c);
                }
            }
            
            var filtered = sb.ToString();

            if (filtered != text)
            {
                _nameInputField.text = filtered;
            }
        }

        public void SetInputData(Action<string> ok, Action cancel)
        {
            _ok = ok;
            _cancel = cancel;
        }

        private void Reset()
        {
            _ok = null;
            _cancel = null;

            _nameInputField.text = "";
        }

        private void OnOkButtonClicked() => _ok?.Invoke(_nameInputField.text);

        private void OnCancelButtonClicked() => _cancel?.Invoke();
    }
}