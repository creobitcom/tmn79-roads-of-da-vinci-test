using Creobit.Bootstrap.Core.Scripts.Runtime.Analytics;
using Creobit.Localization;
using Creobit.UI;
using Cysharp.Threading.Tasks;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Settings
{
    public class SettingsPanelView : PanelData, ISelfValidator
    {
        [SerializeField] private Button _disableMusicButton;

        [SerializeField] private Button _disableSfxButton;

        [SerializeField] private Slider _musicVolumeSlider;

        [SerializeField] private Slider _sfxVolumeSlider;

        [SerializeField] private Toggle _isFullScreenToggle;

        [SerializeField] private Toggle _isSystemCursor;

        [SerializeField] private Image _localizationButtonImage;
        
        [SerializeField] private LocalizationButtonImagesSO _localizationButtonImages;

        [SerializeField] private Image _musicIcon;
        
        [SerializeField] private Image _soundIcon;
        
        [SerializeField] private SfxSpritesData _sfxSprites;

        private IGameSettingsController _gameSettingsController;

        private bool _musicIsEnabled => _musicVolumeSlider.value > 0;
        private bool _sfxIsEnabled => _sfxVolumeSlider.value > 0;

        public void Validate(SelfValidationResult result)
        {
            if (_musicVolumeSlider != null && (_musicVolumeSlider.minValue < 0 || _musicVolumeSlider.maxValue > 1))
            {
                result.AddError($"MusicVolumeSlider: min value must be at least 0, and max value must be no more than 1");
            }

            if (_sfxVolumeSlider != null && (_sfxVolumeSlider.minValue < 0 || _sfxVolumeSlider.maxValue > 1))
            {
                result.AddError($"SfxVolumeSlider: min value must be at least 0, and max value must be no more than 1");
            }
        }

        [Inject]
        private void Construct(IGameSettingsController gameSettingsController)
        {
            _gameSettingsController = gameSettingsController;
        }

        public override UniTask Load()
        {
            Observable
                .EveryValueChanged(gameObject, x => x != null && x.activeSelf) // detect if panel open or close
                .Skip(1) // skip initialization
                .TakeUntil(destroyCancellationToken)
                .Subscribe(OnPanelActiveStateChanged);
            
            LocalizationService.Instance.OnLanguageChanged += OnLanguageChanged;

            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            UnSubscribeFromEvents();

            LocalizationService.Instance.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnDestroy()
        {
            LocalizationService.Instance.OnLanguageChanged -= OnLanguageChanged;
        }

        private void UnSubscribeFromEvents()
        {
            if (_disableMusicButton != null)
            {
                _disableMusicButton.onClick.RemoveListener(OnDisableMusicButtonClicked);
            }

            if (_disableSfxButton != null)
            {
                _disableSfxButton.onClick.RemoveListener(OnDisableSfxButtonClicked);
            }

            if (_musicVolumeSlider != null)
            {
                _musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeValueChanged);
            }

            if (_sfxVolumeSlider != null)
            {
                _sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeValueChanged);
            }

            if (_isFullScreenToggle != null)
            {
                _isFullScreenToggle.onValueChanged.RemoveListener(OnIsFullScreenValueChanged);
            }

            if (_isSystemCursor != null)
            {
                _isSystemCursor.onValueChanged.RemoveListener(OnIsSystemCursorValueChanged);
            }
        }

        private void OnPanelActiveStateChanged(bool isActive)
        {
            if (!isActive)
            {
                UnSubscribeFromEvents();
                return;
            }

            AnalyticsRecorder.Instance?.SettingsPanelOpened();

            _musicVolumeSlider.value = _gameSettingsController.Settings.MusicVolume;
            _sfxVolumeSlider.value = _gameSettingsController.Settings.SfxVolume;
            
            _musicIcon.sprite = _musicIsEnabled ? _sfxSprites.EnabledMusicSprite : _sfxSprites.MutedMusicSprite;
            _soundIcon.sprite = _sfxIsEnabled ? _sfxSprites.EnabledSoundSprite : _sfxSprites.MutedSoundSprite;

            if (_isFullScreenToggle != null)
            {
                _isFullScreenToggle.isOn = _gameSettingsController.Settings.IsFullScreen;
            }

            if (_isSystemCursor != null)
            {
                _isSystemCursor.isOn = _gameSettingsController.Settings.IsSystemCursor;
            }

            _disableMusicButton.interactable = _musicIsEnabled;
            _disableSfxButton.interactable = _sfxIsEnabled;

            _disableMusicButton.onClick.AddListener(OnDisableMusicButtonClicked);
            _disableSfxButton.onClick.AddListener(OnDisableSfxButtonClicked);

            _musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeValueChanged);
            _sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeValueChanged);

            if (_isFullScreenToggle != null)
            {
                _isFullScreenToggle.onValueChanged.AddListener(OnIsFullScreenValueChanged);
            }

            if (_isSystemCursor != null)
            {
                _isSystemCursor.onValueChanged.AddListener(OnIsSystemCursorValueChanged);
            }

            ChangeLocalizationImage();
        }

        private void ChangeLocalizationImage()
        {
            if (this == null || _localizationButtonImage == null)
            {
                return;
            }

            Sprite currentLocalizationSprite = null;
            foreach (var localizationButtonData in _localizationButtonImages.LocalizationButtonData)
            {
                if (localizationButtonData.Locale == LocalizationService.Instance.CurrentLanguage)
                {
                    currentLocalizationSprite = localizationButtonData.Image;
                    break;
                }
            }
            _localizationButtonImage.sprite = currentLocalizationSprite;
        }

        private void OnLanguageChanged(string obj)
        {
            ChangeLocalizationImage();
        }

        private void OnDisableMusicButtonClicked()
        {
            _musicVolumeSlider.value = 0;
        }

        private void OnDisableSfxButtonClicked()
        {
            _sfxVolumeSlider.value = 0;
        }

        private void OnMusicVolumeValueChanged(float value)
        {
            _gameSettingsController.Settings.MusicVolume = value;

            _disableMusicButton.interactable = _musicIsEnabled;

            _musicIcon.sprite = _musicIsEnabled ? _sfxSprites.EnabledMusicSprite : _sfxSprites.MutedMusicSprite;
        }

        private void OnSfxVolumeValueChanged(float value)
        {
            _gameSettingsController.Settings.SfxVolume = value;

            _disableSfxButton.interactable = _sfxIsEnabled;
            
            _soundIcon.sprite = _sfxIsEnabled ? _sfxSprites.EnabledSoundSprite : _sfxSprites.MutedSoundSprite;
        }

        private void OnIsFullScreenValueChanged(bool value)
        {
            _gameSettingsController.Settings.IsFullScreen = value;
        }

        private void OnIsSystemCursorValueChanged(bool value)
        {
            _gameSettingsController.Settings.IsSystemCursor = value;
        }
    }
}