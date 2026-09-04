using System;
using System.Threading;
using _8floor.TimeManagement.Artifacts.Runtime.Service;
using Creobit.AddressablesController;
using Creobit.Audio;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Creobit.Bootstrap.Core.Scripts.Runtime.AppDataConfigurationSystem;
using Creobit.Bootstrap.Core.Scripts.Runtime.EventsInterceptors;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.Bootstrap.Core.Scripts.Runtime.Settings;
using Creobit.Bootstrap.Core.Scripts.Runtime.Splash;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer.Unity;
using Random = UnityEngine.Random;
using Creobit.Bootstrap.Core.Scripts.Runtime.Fading;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Creobit.EditionsUpgrade;
using Creobit.Fading;
using Creobit.Loading;
using Creobit.Localization;
using Creobit.Logger;
using Creobit.UI;
using Cysharp.Threading.Tasks;
using Creobit.Bootstrap.Core.Scripts.Runtime.Analytics;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Initialization.BootstrapInit
{
    public class BootstrapFlow : IStartable, IDisposable
    {
        private readonly IAppDataConfigurationController _appDataConfigurationController;
        private readonly IAudioService _audioService;
        private readonly IGameSettingsController _gameSettingsController;
        private readonly IPlayerProfilesController _playerProfilesController;
        private readonly IArtifactsService _artifactsService;
        private readonly IFadeController _fadeController;
        private readonly ILoadingController _loadingController;
        private readonly ISaveController _saveController;
        private readonly IPlayerPrefsSaveProvider _playerPrefsSaveProvider;
        private readonly ISplashController _splashController;
        private readonly IUIController _uiController;
        private readonly BootstrapPrefabReferences _prefabReferences;
        
        private readonly GameSwitcher _gameSwitcher;

        private readonly ApplicationEventsInterceptor _applicationEventsInterceptor;
        private readonly IAddressablesController _addressablesController;
        
        private readonly IPrivacyPolicyManager _privacyPolicyManager;
        private readonly AnalyticsRecorder _analyticsRecorder;

        private readonly FadeLoadingEventsListener _fadeLoadingEventsListener;

        public BootstrapFlow(ILoadingController loadingController,
            IAudioService audioService,
            IGameSettingsController gameSettingsController,
            IPlayerProfilesController playerProfilesController,
            IArtifactsService artifactsService,
            IFadeController fadeController,
            IAppDataConfigurationController appDataConfigurationController,
            IPlayerPrefsSaveProvider playerPrefsSaveProvider,
            ISaveController saveController,
            ISplashController splashController,
            IPrivacyPolicyManager privacyPolicyManager,
            ApplicationEventsInterceptor applicationEventsInterceptor,
            IUIController uiController,
            BootstrapPrefabReferences prefabReferences,
            IAddressablesController addressablesController,
            GameSwitcher gameSwitcher,
            AnalyticsRecorder analyticsRecorder)
        {
            _loadingController = loadingController;
            _audioService = audioService;
            _gameSettingsController = gameSettingsController;
            _playerProfilesController = playerProfilesController;
            _artifactsService = artifactsService;
            _fadeController = fadeController;
            _appDataConfigurationController = appDataConfigurationController;
            _saveController = saveController;
            _playerPrefsSaveProvider = playerPrefsSaveProvider;
            _splashController = splashController;
            _privacyPolicyManager = privacyPolicyManager;
            _applicationEventsInterceptor = applicationEventsInterceptor;
            _uiController = uiController;
            _prefabReferences = prefabReferences;
            _addressablesController = addressablesController;
            _gameSwitcher = gameSwitcher;
            _analyticsRecorder = analyticsRecorder;

            _fadeLoadingEventsListener = new(_fadeController, _loadingController);
        }

        public async void Start()
        {
            try
            {
                await _privacyPolicyManager.Init();
                _analyticsRecorder.StartAfterConsent().Forget();
                var fadeView =
                    await _addressablesController.LoadAssetByReferenceAsync<GameObject>(_prefabReferences.FadeView);
                await _fadeController.Init(fadeView);
                
                _fadeLoadingEventsListener.Init();
                
                var loadingView = await _addressablesController.LoadAssetByReferenceAsync<GameObject>(_prefabReferences
                    .LoadingView);
                
                await _loadingController.Init(loadingView);

                AudioSource musicSource = await _prefabReferences.MusicAudioSource.InstantiateAsync(CancellationToken.None);
                AudioSource sfxSource = await _prefabReferences.SfxAudioSource.InstantiateAsync(CancellationToken.None);

                _audioService.Init(_prefabReferences.AudioMixer, musicSource, sfxSource);
                
                _loadingController.AddServicesToLoad
                (
                    _applicationEventsInterceptor,
                    _playerPrefsSaveProvider,
                    _saveController,
                    _appDataConfigurationController
                );
                
                _loadingController.AddServicesToLoadWith(_prefabReferences.PlayerProfilesRules, _playerProfilesController);

                _loadingController.AddServicesToLoad
                (
                    _artifactsService,
                    _gameSettingsController,
                    _splashController
                );
      
                
                var persistentUIObjectPrefab = await _addressablesController
                    .LoadAssetByReferenceAsync<GameObject>(_prefabReferences.PersistentUICanvas);

                var persistentUIObject = await persistentUIObjectPrefab.InstantiateAsync(cancellationToken: CancellationToken.None);
                
                _loadingController.AddServicesToLoadWith(persistentUIObject, _uiController);
              

#if CREOBIT_PROD_PROD
            Debug.unityLogger.filterLogType = LogType.Warning;
            Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.ScriptOnly);
            Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
            Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);
#endif

                Random.InitState(DateTime.UtcNow.GetHashCode());

#if UNITY_IOS
                UnityEngine.iOS.Device.hideHomeButton = true;
#endif

                var language = _playerPrefsSaveProvider.TryGetValue(RuntimeConstants.GameSettingsPrefs.Language);

                if (string.IsNullOrEmpty(language))
                {
                    language = ResolveSystemLanguage();
                    _playerPrefsSaveProvider.Save(RuntimeConstants.GameSettingsPrefs.Language, language);
                }

                await LocalizationService.Instance.Init(
                    "RoadsOfDaVinci",
                    language,
                    RuntimeConstants.LocalizationParserData.SupportedLanguages);

                await _loadingController
                    .ExecuteAllLoadingTasks(false);

                var splashTask = _splashController.Show();
                //CHANGED
                
                _gameSwitcher.SetCurrentGame("RoadsOfDaVinci");
                _loadingController.ScheduleSceneLoading(RuntimeConstants.SceneIndex.MetaScene);

                // Warm Meta UI in background during splash. Never block Meta transition:
                // in player builds awaiting all preloads can hang Addressables and leave Bootstrap forever.
                PreloadMetaUiAssets().Forget();
                await splashTask;
                await _loadingController.LoadScheduledScene();
            }
            catch (Exception e)
            {
                Log.Bootstrap.Error(e);
            }
        }

        private static string ResolveSystemLanguage()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Russian: return "ru-RU";
                case SystemLanguage.German: return "de-DE";
                case SystemLanguage.Spanish: return "es-ES";
                case SystemLanguage.French: return "fr-FR";
                case SystemLanguage.Italian: return "it-IT";
                case SystemLanguage.Dutch: return "nl-NL";
                case SystemLanguage.Polish: return "pl-PL";
                case SystemLanguage.Portuguese: return "pt-BR";
                default: return RuntimeConstants.LocalizationParserData.DefaultLanguage;
            }
        }

        private async UniTask PreloadMetaUiAssets()
        {
            var assets = _prefabReferences.MetaUiPreloadAssets;
            if (assets == null || assets.Length == 0)
            {
                return;
            }

            // Sequential: parallel Addressables.LoadAssetAsync is flaky in player builds.
            for (var i = 0; i < assets.Length; i++)
            {
                await PreloadMetaUiAsset(assets[i]);
            }
        }

        private async UniTask PreloadMetaUiAsset(AssetReference asset)
        {
            if (asset == null || !asset.RuntimeKeyIsValid())
            {
                return;
            }

            try
            {
                await _addressablesController.LoadAssetByReferenceAsync<GameObject>(asset);
            }
            catch (Exception e)
            {
                Log.Bootstrap.Warning($"Meta UI preload failed for {asset.AssetGUID}: {e.Message}");
            }
        }

        public void Dispose()
        {
            _fadeLoadingEventsListener.Dispose();
        }
    }
}