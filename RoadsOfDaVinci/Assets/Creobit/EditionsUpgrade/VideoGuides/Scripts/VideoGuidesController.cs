using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    public class VideoGuidesController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private VideoLoader _videoLoader;

        [Header("Options")]
        [SerializeField] private string _folderToGuides;
        [SerializeField] private int _countGuides = 60;
        [SerializeField] private string _defaultVideoUrl = "https://go.8floor.net/walkthrough/roads-of-da-vinci-level-";
        [SerializeField] private string _levelFolderLabel = "Level";

        [Header("Confirmation")]
        [SerializeField, Tooltip("Turn on if options corrected")]
        private bool _isPrepared;

        [Header("Runtime options")]
        [SerializeField] private GameObject[] _disabledObjects;


        public bool IsExistsVideo { get; private set; }
        public bool IsLoadingVideo { get; private set; }

        public event Action OnSuccess;
        public event Action OnFail;
        public event Action OnError;
        public event Action OnLoadStateChanged;

        private Config _runtimeConfig;
        private Guide _selectedGuide;

        private const string LevelGuideLabel = "Guide.json";

        private int _levelNumber;
        private Task _initialization;

        private string _runtimeConfigPath;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_isPrepared == false)
            {
                return;
            }

            TryCreateConfig();

            TryCreateGuides();

            void TryCreateConfig()
            {
                var configPath = Path.Combine(Application.streamingAssetsPath, _folderToGuides, "Config.json");

                if (!File.Exists(configPath))
                {
                    if (!EditorUtility.DisplayDialog(
                        "Confirmation",
                        "Configuration videoguides file not found. Do you want to create a new one?",
                        "Yes",
                        "Cancel"))
                    {
                        Debug.Log("File creation was canceled by the user.");
                        return;
                    }

                    JsonService.Create(configPath, new Config
                    {
                        DefaultVideoUrl = _defaultVideoUrl,
                        UseLocalVideo = false,
                        UseYoutubeUrls = true
                    });

                    Debug.Log("Created videoguides config at path: " + configPath);
                }
            }
            void TryCreateGuides()
            {
                var pathGuides = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, _folderToGuides));

                if (!Directory.Exists(pathGuides))
                {
                    Debug.LogError($"Path {pathGuides} is not exists to guides folder.");
                    return;
                }

                for (int i = 1; i <= _countGuides; i++)
                {
                    string levelFolderName = $"{_levelFolderLabel}{i:D2}";
                    var levelPath = Path.Combine(pathGuides, levelFolderName);

                    if (!Directory.Exists(levelPath))
                    {
                        Directory.CreateDirectory(levelPath);
                        Debug.Log($"Created guide folder: {levelPath}");
                    }

                    var guidePath = Path.Combine(levelPath, LevelGuideLabel);

                    if (!File.Exists(guidePath))
                    {
                        var guide = new Guide
                        {
                            VideoUrl = ""
                        };

                        JsonService.Create(guidePath, guide);
                        Debug.Log($"Created Guide: {guidePath}");
                    }
                }
            }
        }
#endif

        private void OnDestroy()
        {
            _videoLoader.OnStartedPlaying -= VideoPlayerStarted;

            OnFail -= _videoLoader.Pause;
            OnSuccess -= _videoLoader.Play;
        }

        private void Awake()
        {
            _videoLoader.OnStartedPlaying += VideoPlayerStarted;

            OnFail += _videoLoader.Pause;
            OnSuccess += _videoLoader.Play;

            Initialize();
        }

        private void OnDisable()
        {
            _videoLoader.Pause();
        }

        public void Initialize()
        {
            EnsureInitialized();
        }

        public async void LoadLevel(int levelNumber)
        {
            if (_levelNumber == levelNumber) return;

            _levelNumber = levelNumber;

            SetVideoState(true, false);

            await EnsureInitialized();

            _selectedGuide = await GetGuideByNumber(levelNumber);

#if UNITY_STANDALONE || (UNITY_STANDALONE && UNITY_EDITOR)
            if (_runtimeConfig != null && _runtimeConfig.UseLocalVideo)
            {
                TryLoadLocalVideo();
            }
            else
            {
                VideoMissingHandler();
            }
#elif UNITY_ANDROID || UNITY_IOS || (UNITY_ANDROID && UNITY_EDITOR)
            VideoMissingHandler();
#endif
        }

        private Task EnsureInitialized()
        {
            if (_initialization != null) return _initialization;

            _initialization = InitializeAsync();

            return _initialization;
        }

        private async Task InitializeAsync()
        {
            _runtimeConfigPath = Path.Combine(Application.streamingAssetsPath, _folderToGuides, "Config.json");

            _runtimeConfig = await JsonService.LoadAsync<Config>(_runtimeConfigPath, () => OnError?.Invoke());
        }

        private void SetVideoState(bool isLoading, bool isExists)
        {
            IsLoadingVideo = isLoading;
            IsExistsVideo = isExists;

            OnLoadStateChanged?.Invoke();
        }

        private void VideoMissingHandler()
        {
            SetVideoState(false, false);

            OnFail?.Invoke();
        }

        public void OpenYoutubeVideo()
        {
#if GOOGLE_PLAY || UNITY_IOS || MAC_APPSTORE
            var link = string.IsNullOrEmpty(_runtimeConfig.DefaultVideoUrl)
                ? $"{_defaultVideoUrl}{_levelNumber:00}"
                : $"{_runtimeConfig.DefaultVideoUrl}{_levelNumber:00}";
#else
            var link = string.IsNullOrEmpty(_selectedGuide.VideoUrl)
                ? $"{_runtimeConfig.DefaultVideoUrl}{_levelNumber:00}"
                : _selectedGuide.VideoUrl;
#endif

            Application.OpenURL(link);
        }

        public bool IsNeedShowYoutube()
        {
            if (_runtimeConfig?.UseYoutubeUrls == false) return false;

#if GOOGLE_PLAY || UNITY_IOS || MAC_APPSTORE
            return true;
#else
            return string.IsNullOrEmpty(_selectedGuide?.VideoUrl) == false ||
                   string.IsNullOrEmpty(_runtimeConfig?.DefaultVideoUrl) == false;
#endif
        }

        public void Play()
        {
            _videoLoader.Play();

            SetDisabledObjectsState(false);

            OnSuccess?.Invoke();
        }

        public void Pause()
        {
            _videoLoader.Pause();

            SetDisabledObjectsState(true);

            OnFail?.Invoke();
        }

        private async Task<Guide> GetGuideByNumber(int levelNumber)
        {
            string path = Path.Combine(
                Application.streamingAssetsPath,
                _folderToGuides,
                $"{_levelFolderLabel}{levelNumber:00}",
                LevelGuideLabel
            );

            return await JsonService.LoadAsync<Guide>(path);
        }

        private void VideoPlayerStarted()
        {
            SetDisabledObjectsState(IsExistsVideo == false);

            if (IsExistsVideo)
            {
                OnSuccess?.Invoke();
            }
            else
            {
                OnFail?.Invoke();
            }
        }

        private void VideoOpenedHandler()
        {
            _videoLoader.Pause();

            SetDisabledObjectsState(false);
        }

        private void SetDisabledObjectsState(bool state)
        {
            foreach (var item in _disabledObjects)
            {
                item.SetActive(state);
            }
        }

        private async void TryLoadLocalVideo()
        {
            var basePath = Path.Combine(Application.streamingAssetsPath, _folderToGuides);
            var levelPath = Path.Combine(basePath, $"{_levelFolderLabel}{_levelNumber:00}");

            await _videoLoader.LoadAsync(Path.GetFullPath(levelPath), (isLoaded) =>
            {
                SetVideoState(false, isLoaded);

                if (isLoaded)
                {
                    OnSuccess?.Invoke();

                    VideoOpenedHandler();
                }
                else
                {
                    OnFail?.Invoke();
                }
            });
        }

        [Serializable]
        public class Guide
        {
            public string VideoUrl;
        }

        [Serializable]
        public class Config
        {
            public string DefaultVideoUrl;
            public bool UseYoutubeUrls;
            public bool UseLocalVideo;
        }
    }

}