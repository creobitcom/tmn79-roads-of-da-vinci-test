using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Extras;
using Creobit.Audio;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Creobit.Localization;
using Creobit.UI;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras.Views
{
    public class ExtrasPanelLA8View : PanelData
    {
        [SerializeField] private GameObject _artSelectedVisual;
        [SerializeField] private GameObject _artUnselectedVisual;
        [SerializeField] private Button _artTabButton;
        [SerializeField] private GameObject _musicSelectedVisual;
        [SerializeField] private GameObject _musicUnselectedVisual;
        [SerializeField] private Button _musicTabButton;

        [SerializeField] private GameObject _artContainer;
        [SerializeField] private GameObject _musicContainer;

        [SerializeField] private Image _artImage;
        [SerializeField] private Button _prevArtButton;
        [SerializeField] private Button _nextArtButton;
        [SerializeField] private Sprite[] _artSprites;
        [SerializeField] private int _currentArtIndex;

        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private string _musicMixerGroupName = "MusicVolume";
        [SerializeField] private TrackData[] _musicTracks;
        [SerializeField] private ExtrasTrackItemView[] _trackItems;

        [SerializeField] private GameObject _savedPanel;
        [SerializeField] private TMP_Text _savedTitleText;
        [SerializeField] private TMP_Text _savedPathText;
        [SerializeField] private Button _savedPanelOkButton;

        private const string DefaultMusicMixerGroupName = "MusicVolume";

        private IAudioService _audioService;
        private BootstrapPrefabReferences _prefabReferences;
        private ExtrasTrackItemView _currentPlayingItem;

        [Inject]
        public void Construct(IAudioService audioService, BootstrapPrefabReferences prefabReferences)
        {
            _audioService = audioService;
            _prefabReferences = prefabReferences;
        }

        public override UniTask Load()
        {
            ApplyMixerRouting();

            if (_artTabButton != null)
            {
                _artTabButton.onClick.AddListener(OnArtTabClicked);
            }

            if (_musicTabButton != null)
            {
                _musicTabButton.onClick.AddListener(OnMusicTabClicked);
            }

            if (_prevArtButton != null)
            {
                _prevArtButton.onClick.AddListener(OnPrevArtClicked);
            }

            if (_nextArtButton != null)
            {
                _nextArtButton.onClick.AddListener(OnNextArtClicked);
            }

            if (_savedPanelOkButton != null)
            {
                _savedPanelOkButton.onClick.AddListener(OnCloseSavedPanel);
            }

            if (_savedTitleText != null && LocalizationService.Instance != null)
            {
                _savedTitleText.text = LocalizationService.Instance.GetText("UI_soundtracks");
            }

            if (_savedPanel != null)
            {
                _savedPanel.SetActive(false);
            }

            InitializeTracks();
            SwitchTab(true);
            SetArtIndex(0);

            return base.Load();
        }

        private void ApplyMixerRouting()
        {
            if (_musicSource == null)
            {
                return;
            }

            var mixer = _prefabReferences?.AudioMixer;

            if (mixer == null)
            {
                return;
            }

            var groupName = string.IsNullOrEmpty(_musicMixerGroupName)
                ? DefaultMusicMixerGroupName
                : _musicMixerGroupName;

            var groups = mixer.FindMatchingGroups(groupName);

            if (groups == null || groups.Length == 0)
            {
                Debug.LogWarning($"[Extras] Mixer group {groupName} not found in {mixer.name}.");
                return;
            }

            _musicSource.outputAudioMixerGroup = groups[0];
        }

        private void InitializeTracks()
        {
            if (_trackItems == null) return;

            for (var i = 0; i < _trackItems.Length; i++)
            {
                var item = _trackItems[i];
                if (item != null && _musicTracks != null && i < _musicTracks.Length)
                {
                    item.Initialize(_musicTracks[i], i);
                    item.OnPlayRequested += OnPlayTrack;
                    item.OnPauseRequested += OnPauseTrack;
                    item.OnDownloadFinished += OnTrackDownloaded;
                }
            }
        }

        private void OnTrackDownloaded(string path)
        {
            if (_savedPathText != null)
            {
                if (LocalizationService.Instance != null)
                {
                    var format = LocalizationService.Instance.GetText("UI_soundtrack_confirmation");
                    if (!string.IsNullOrEmpty(format) && format.Contains("%PATH%"))
                    {
                        _savedPathText.text = format.Replace("%PATH%", path);
                    }
                    else
                    {
                        _savedPathText.text = $"Saved at:\n{path}";
                    }
                }
                else
                {
                    _savedPathText.text = $"Saved at:\n{path}";
                }
            }

            if (_savedPanel != null)
            {
                _savedPanel.SetActive(true);
            }
        }

        private void OnCloseSavedPanel()
        {
            if (_savedPanel != null)
            {
                _savedPanel.SetActive(false);
            }
        }

        private void OnArtTabClicked()
        {
            SwitchTab(true);
        }

        private void OnMusicTabClicked()
        {
            SwitchTab(false);
        }

        public void SwitchTab(bool isArt)
        {
            if (_artSelectedVisual != null) _artSelectedVisual.SetActive(isArt);
            if (_artUnselectedVisual != null) _artUnselectedVisual.SetActive(!isArt);
            if (_musicSelectedVisual != null) _musicSelectedVisual.SetActive(!isArt);
            if (_musicUnselectedVisual != null) _musicUnselectedVisual.SetActive(isArt);

            if (_artContainer != null) _artContainer.SetActive(isArt);
            if (_musicContainer != null) _musicContainer.SetActive(!isArt);
        }

        private void OnPrevArtClicked()
        {
            SetArtIndex(_currentArtIndex - 1);
        }

        private void OnNextArtClicked()
        {
            SetArtIndex(_currentArtIndex + 1);
        }

        private void SetArtIndex(int index)
        {
            if (_artSprites == null || _artSprites.Length == 0)
            {
                return;
            }

            _currentArtIndex = Mathf.Clamp(index, 0, _artSprites.Length - 1);

            if (_artImage != null)
            {
                _artImage.sprite = _artSprites[_currentArtIndex];
            }

            if (_prevArtButton != null)
            {
                var isVisible = _currentArtIndex > 0;
                _prevArtButton.interactable = isVisible;
                _prevArtButton.gameObject.SetActive(isVisible);
            }

            if (_nextArtButton != null)
            {
                var isVisible = _currentArtIndex < _artSprites.Length - 1;
                _nextArtButton.interactable = isVisible;
                _nextArtButton.gameObject.SetActive(isVisible);
            }
        }

        private void OnPlayTrack(ExtrasTrackItemView trackItem)
        {
            if (trackItem == null || trackItem.TrackData == null || trackItem.TrackData.MusicClip == null)
            {
                return;
            }

            foreach (var item in _trackItems)
            {
                if (item != null && item != trackItem)
                {
                    item.SetPlaying(false);
                }
            }

            _currentPlayingItem = trackItem;

            if (_musicSource != null)
            {
                _musicSource.clip = trackItem.TrackData.MusicClip;
                _musicSource.Play();
            }

            trackItem.SetPlaying(true);

            if (_audioService != null)
            {
                _audioService.PauseMusic();
            }
        }

        private void OnPauseTrack(ExtrasTrackItemView trackItem)
        {
            if (_musicSource != null)
            {
                _musicSource.Pause();
            }

            if (trackItem != null)
            {
                trackItem.SetPlaying(false);
            }

            if (_audioService != null)
            {
                _audioService.ResumeMusic();
            }

            if (_currentPlayingItem == trackItem)
            {
                _currentPlayingItem = null;
            }
        }

        private void StopAllPlayback()
        {
            if (_musicSource != null && _musicSource.isPlaying)
            {
                _musicSource.Stop();
            }

            if (_trackItems != null)
            {
                foreach (var item in _trackItems)
                {
                    if (item != null)
                    {
                        item.SetPlaying(false);
                    }
                }
            }

            if (_currentPlayingItem != null)
            {
                _currentPlayingItem = null;
                if (_audioService != null)
                {
                    _audioService.ResumeMusic();
                }
            }
        }

        private void OnDisable()
        {
            StopAllPlayback();
        }

        private void OnDestroy()
        {
            StopAllPlayback();

            if (_artTabButton != null)
            {
                _artTabButton.onClick.RemoveListener(OnArtTabClicked);
            }

            if (_musicTabButton != null)
            {
                _musicTabButton.onClick.RemoveListener(OnMusicTabClicked);
            }

            if (_prevArtButton != null)
            {
                _prevArtButton.onClick.RemoveListener(OnPrevArtClicked);
            }

            if (_nextArtButton != null)
            {
                _nextArtButton.onClick.RemoveListener(OnNextArtClicked);
            }

            if (_savedPanelOkButton != null)
            {
                _savedPanelOkButton.onClick.RemoveListener(OnCloseSavedPanel);
            }

            if (_trackItems != null)
            {
                foreach (var item in _trackItems)
                {
                    if (item != null)
                    {
                        item.OnPlayRequested -= OnPlayTrack;
                        item.OnPauseRequested -= OnPauseTrack;
                        item.OnDownloadFinished -= OnTrackDownloaded;
                    }
                }
            }
        }
    }
}
