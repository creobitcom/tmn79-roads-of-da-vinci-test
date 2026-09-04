using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Audio;
using Creobit.Bootstrap.Core.Scripts.Runtime.EventsInterceptors;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.UI;
using Cysharp.Threading.Tasks;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using Log = Creobit.Logger.Log;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController
{
    public class LevelController : MonoBehaviour, ILevelController
    {
        private readonly ReactiveProperty<bool> _isLevelStarted = new(false);
        
        private bool _isLevelFinished;

        private IPauseController _pauseController;
        private ILevelLoader _levelLoader;
        private IGameplayInputSystem _gameplayInputSystem;
        private GameplaySceneReferences _gameplaySceneReferences;
        private IReloadController _reloadController;
        private IAudioService _audioService;
        private IUIController _uiController;
        private ApplicationEventsInterceptor _applicationEventsInterceptor;

        private LevelBaseSO _currentLevelData;
        private bool _isPauseViewShown;

        public ReadOnlyReactiveProperty<bool> IsLevelStarted => _isLevelStarted;
        public event Action FinishScreenShowed;
        public event Action WinViewShowing;
        public event Action<LevelBaseSO> LevelFinished;
        public event Action DialogueStarted;

        [Inject]
        public void Construct(ILevelLoader levelLoader,
            IPauseController pauseController,
            GameplaySceneReferences gameplaySceneReferences,
            IGameplayInputSystem gameplayInputSystem,
            IReloadController reloadController,
            IAudioService audioService,
            IUIController uiController,
            ApplicationEventsInterceptor applicationEventsInterceptor)
        {
            _pauseController = pauseController;
            _levelLoader = levelLoader;
            _gameplaySceneReferences = gameplaySceneReferences;
            _gameplayInputSystem = gameplayInputSystem;
            _reloadController = reloadController;
            _audioService = audioService;
            _uiController = uiController;
            _applicationEventsInterceptor = applicationEventsInterceptor;
        }

        public UniTask Load()
        {
            _ = _levelLoader.LevelBaseSO.Skip(1)
                .Subscribe(LevelDataLoadedHandler)
                .AddTo(this);

            _levelLoader.LevelLoaded += LevelLoaded;
            _gameplayInputSystem.OnPauseButtonPressed += PauseResume;
#if !UNITY_EDITOR
            _applicationEventsInterceptor.FocusStateChanged += OnApplicationFocusChanged;
#endif

            return UniTask.CompletedTask;
        }

        [Button]
        public void StartLevel()
        {
            _isLevelStarted.Value = true;
            _gameplayInputSystem.IsActionAvailable = true;

            _gameplayInputSystem.Enable();
        }

        [Button]
        public void FinishLevel()
        {
            _gameplayInputSystem.Disable();
            _isLevelFinished = true;

            LevelFinished?.Invoke(_currentLevelData);
        }

        public void NotifyWinViewShowing()
        {
            WinViewShowing?.Invoke();
        }

        public void Dispose()
        {
            _levelLoader.LevelLoaded -= LevelLoaded;
            _gameplayInputSystem.OnPauseButtonPressed -= PauseResume;
#if !UNITY_EDITOR
            _applicationEventsInterceptor.FocusStateChanged -= OnApplicationFocusChanged;
#endif
        }

        [Button]
        public void Pause()
        {
            _pauseController.Pause(); 
        }

        [Button]
        public void Resume()
        {
            _pauseController.Resume();
        }

#if !UNITY_EDITOR
        private void OnApplicationFocusChanged(bool hasFocus)
        {
            if (hasFocus)
            {
                return;
            }

            if (!IsLevelStarted.CurrentValue || _isLevelFinished || _pauseController.IsPaused.CurrentValue) return;

            ShowPauseView();
            Pause();
        }
#endif

        private void PauseResume()
        {
            if (_pauseController.IsPaused.CurrentValue)
            {
                HidePauseView();
                Resume();
            }
            else
            {
                ShowPauseView();
                Pause();
            }
        }

        private void ShowPauseView()
        {
            _uiController.ShowPanel(_gameplaySceneReferences.PauseView, null);

            _isPauseViewShown = true;
        }

        private void HidePauseView()
        {
            if (!_isPauseViewShown)
            {
                return;
            }

            _uiController.HidePanel(_gameplaySceneReferences.PauseView, null);

            _isPauseViewShown = false;
        }

        public void DisableInput()
        {
            _gameplayInputSystem.Disable();
        }

        public void EnableInput()
        {
            _gameplayInputSystem.Enable();
        }

        public void PrepareForNewLevel()
        {
            HidePauseView();

            Resume();
            _isLevelFinished = false;
            _isLevelStarted.Value = false;

            if (_gameplaySceneReferences != null && _gameplaySceneReferences.FinishLevelObject != null)
            {
                _gameplaySceneReferences.FinishLevelObject.SetActive(false);
            }

            SetPressAnythingToContinueObjectState(false);
        }

        [Button]
        public void ReloadLevel()
        {
            PrepareForNewLevel();
            _reloadController.ReloadLevel();
        }

        public void SetPressAnythingToContinueObjectState(bool state)
        {
            if (_gameplaySceneReferences?.PressAnythingToContinueObject == null)
            {
                return;
            }

            _gameplaySceneReferences.PressAnythingToContinueObject.gameObject.SetActive(state);
        }

        public void ShowFinishScreen()
        {
            _gameplaySceneReferences.FinishLevelObject.SetActive(true);

            FinishScreenShowed?.Invoke();
        }

        public void StartDialogue()
        {
            DialogueStarted?.Invoke();
        }

        private void LevelDataLoadedHandler(LevelBaseSO levelBaseSo)
        {
            _currentLevelData = levelBaseSo;
        }

        private void LevelLoaded()
        {
            _gameplayInputSystem.Disable();
            SetInitialLevelState();
        }

        private void SetInitialLevelState()
        {
            if (_gameplaySceneReferences == null)
            {
                Log.Gameplay.Error("GameplaySceneReferences is null.");
                return;
            }

            if (_currentLevelData == null)
            {
                Log.Gameplay.Error("CurrentLevelData is null.");
                return;
            }

            _gameplaySceneReferences.FinishLevelObject?.SetActive(false);

            if (_gameplaySceneReferences.GlobalLight != null)
            {
                _gameplaySceneReferences.GlobalLight.intensity = _currentLevelData.GlobalLightIntensityOnStart;
            }
            else
            {
                Log.Gameplay.Info("GlobalLight is null.");
            }

            if (_gameplaySceneReferences.GlobalDarkVolume != null)
            {
                _gameplaySceneReferences.GlobalDarkVolume.gameObject.SetActive(_currentLevelData
                    .GlobalPostProcessActiveOnStart);
            }
            else
            {
                Log.Gameplay.Info("GlobalDarkVolume is null.");
            }

            if (_audioService != null)
            {
                _audioService.PlayMusic(_currentLevelData.Music);
            }
            else
            {
                Log.Gameplay.Info("AudioService is null");
            }
        }
    }
}