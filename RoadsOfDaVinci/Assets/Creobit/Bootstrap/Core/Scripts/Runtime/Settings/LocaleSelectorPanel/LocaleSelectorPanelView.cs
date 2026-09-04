using System;
using System.Collections.Generic;
using System.Linq;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility;
using Creobit.Localization;
using Creobit.UI;
using Cysharp.Threading.Tasks;
using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Settings
{
    public class LocaleSelectorPanelView : PanelData
    {
        private const float AnimationDelaySeconds = 0f;
        
        [SerializeField] private RectTransform _buttonParentTransform;
        [SerializeField] private Image _markImage;

        private IPlayerPrefsSaveProvider _prefsSaveProvider;
        
        private readonly Dictionary<string, Button> _languageButtons = new();
        private readonly CompositeDisposable _disposables = new();

        [Inject]
        private void Construct(IPlayerPrefsSaveProvider prefsSaveProvider)
        {
            _prefsSaveProvider = prefsSaveProvider;
        }

        public override UniTask Load()
        {
            HideMarkImage();
            InitializeLanguageButtons();
            SubscribeToLanguageChanges();
            SetupButtonClickHandlers();
            SetupPanelStateHandlers();
        
            return UniTask.CompletedTask;
        }

        private void InitializeLanguageButtons()
        {
            var availableLanguages = LocalizationService.Instance.AvailableLanguages;
        
            foreach (Transform buttonTransform in _buttonParentTransform)
            {
                if (!_languageButtons.ContainsKey(buttonTransform.name) && availableLanguages.Contains(buttonTransform.name))
                {
                    _languageButtons.Add(buttonTransform.name, buttonTransform.GetComponent<Button>());
                }
                else
                {
                    buttonTransform.gameObject.SetActive(false);
                }
            }
        }

        private void SubscribeToLanguageChanges()
        {
            LocalizationService.Instance.OnLanguageChanged += UpdateMarkImagePosition;
        }

        private void SetupButtonClickHandlers()
        {
            foreach (var (language, button) in _languageButtons)
            {
                button.OnClickAsObservable()
                     .Subscribe(async _ => await HandleLanguageSelection(language))
                     .AddTo(_disposables);
            }
        }

        private void SetupPanelStateHandlers()
        {
            this.OnEnableAsObservable()
                .Delay(TimeSpan.FromSeconds(AnimationDelaySeconds))
                .Subscribe(_ => UpdateMarkImagePosition(LocalizationService.Instance.CurrentLanguage))
                .AddTo(_disposables);

            this.OnDisableAsObservable()
                .Subscribe(_ => HideMarkImage())
                .AddTo(_disposables);
        }

        private async UniTask HandleLanguageSelection(string language)
        {
            await LocalizationService.Instance.LoadAndSetLanguage(language);
            UpdateMarkImagePosition(language);
            _prefsSaveProvider.Save(RuntimeConstants.GameSettingsPrefs.Language, language);
        }

        private void UpdateMarkImagePosition(string language)
        {
            if (!_languageButtons.TryGetValue(language, out var button))
                return;
            
            _markImage.transform.position = button.transform.position;
            ShowMarkImage();
        }

        private void ShowMarkImage() => _markImage.gameObject.SetActive(true);
        private void HideMarkImage() => _markImage.gameObject.SetActive(false);

        public override void Dispose()
        {
            LocalizationService.Instance.OnLanguageChanged -= UpdateMarkImagePosition;
        
            foreach (var (_, button) in _languageButtons)
            {
                button.onClick.RemoveAllListeners();
            }
        
            _disposables.Clear();
        }
    }
}