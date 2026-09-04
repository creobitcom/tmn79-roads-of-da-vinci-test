using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Systems;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer.View;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer
{
    public class LevelTimer : ILevelTimer
    {
        private readonly CompositeDisposable _disposable = new();

        private ILevelController _levelController;
        private ILevelLoader _levelLoader;
        private IReloadController _reloadController;
        private IGameplayIntervalsController _gameplayIntervalsController;
        private IPlayerProfilesController _playerProfilesController;
        private ISaveController _saveController;
        private GameplaySceneReferences _gameplaySceneReferences;

        private GameplayIntervalGeneralParameters _gameplayIntervalGeneralParameters;
        private TimerProgressView _timerProgressView;

        private LevelStarData[] _levelStarData;

        private float _currentTimer;

        private ushort _currentTimerId;

        private bool _timerIsPaused;

        // Источников паузы может быть несколько: лёгкий режим и окна туториала с pauseLevelTimer.
        // Считаем их отдельно, иначе туториал, закрываясь, запустил бы таймер, который до него
        // остановил лёгкий режим.
        private readonly HashSet<object> _pauseSources = new();

        private static readonly object GameModePauseSource = new();

        public uint Speed { get; set; } = 1;

        private bool EasyModeGivesMaxStars =>
            _gameplaySceneReferences.GameplaySettings == null ||
            _gameplaySceneReferences.GameplaySettings.EasyModeGivesMaxStars;

        private bool IsEasyMode => _saveController.CurrentSaveData.ProfileData.GameMode == GameMode.Easy;

        [Inject]
        private void Construct(ILevelController levelController,
            ILevelLoader levelLoader,
            IReloadController reloadController,
            IGameplayIntervalsController gameplayIntervalsController,
            GameplaySceneReferences gameplaySceneReferences,
            IPlayerProfilesController playerProfilesController,
            ISaveController saveController)
        {
            _levelController = levelController;
            _levelLoader = levelLoader;
            _reloadController = reloadController;
            _gameplayIntervalsController = gameplayIntervalsController;
            _gameplaySceneReferences = gameplaySceneReferences;
            _playerProfilesController = playerProfilesController;
            _saveController = saveController;
        }

        private void TimerStarted()
        {
            _currentTimer = (uint)(_gameplayIntervalGeneralParameters.DurationSeconds);
        }

        private void TimerTicked()
        {
            if (_currentTimer == 0)
            {
                StopLevelTimer();
                return;
            }

            var unit = 1f * Speed;
            _currentTimer = (uint)Mathf.Max(0, _currentTimer - unit);

            _timerProgressView.Report(_currentTimer);
        }

        private void TimerCompleted()
        {
            _ = EvaluateTimer();
        }

        private void SetTimerPauseState(bool isPause) => SetPauseSource(GameModePauseSource, isPause);

        public void PauseLevelTimer(object source) => SetPauseSource(source, true);

        public void ResumeLevelTimer(object source) => SetPauseSource(source, false);

        private void SetPauseSource(object source, bool isPause)
        {
            if (source == null)
            {
                return;
            }

            if (isPause)
            {
                _pauseSources.Add(source);
            }
            else
            {
                _pauseSources.Remove(source);
            }

            ApplyPauseState();
        }

        private void ApplyPauseState()
        {
            var isPause = _pauseSources.Count > 0;

            // Skip update if the pause state hasn't changed to avoid unnecessary Pause/Unpause calls
            if (_timerIsPaused == isPause)
            {
                return;
            }

            _timerIsPaused = isPause;

            if (isPause)
            {
                _gameplayIntervalsController.PauseInterval(_currentTimerId);
                return;
            }

            _gameplayIntervalsController.UnPauseInterval(_currentTimerId);
        }

        private void LevelDataLoadedHandler(LevelBaseSO levelBase)
        {
            var isEasyMode = IsEasyMode;

            _timerProgressView.SetEasyMode(isEasyMode);
            _timerProgressView.gameObject.SetActive(!isEasyMode || _timerProgressView.HasEasyModeVisuals);

            _levelStarData = levelBase.LevelTimerData.LevelStarsData;

            SetupLevelTimer(levelBase.LevelTimerData);
        }

        private void LevelStartedHandler(bool isStarted)
        {
            if (!isStarted)
            {
                StopLevelTimer();
                return;
            }

            StartLevelTimer();

            SetTimerPauseState(IsEasyMode);
        }

        private void LevelFinishedHandler(LevelBaseSO levelBase)
        {
            int starsCount = EvaluateTimer().NumberOfStars;
            _saveController.Service.SaveLevel(levelBase.LevelNumber, starsCount);

            _saveController.Service.SaveLastPassedLevel(levelBase.LevelNumber);

            _saveController.Service.TrySaveLastUnlockedLevel(levelBase.LevelNumber + 1);
        }

        private void GameModeChangedHandler() => SetTimerPauseState(IsEasyMode);

        public void SetupLevelTimer(LevelTimerData levelTimerData)
        {
            _timerProgressView.SetData(levelTimerData);

            _gameplayIntervalGeneralParameters = levelTimerData.GameplayIntervalGeneralParameters;
        }

        public void StartLevelTimer()
        {
            _currentTimerId = _gameplayIntervalsController.StartInterval(
                new GameplayIntervalSpecificParameters(
                    TimerStarted,
                    null,
                    null,
                    null,
                    TimerTicked,
                    null,
                    null,
                    TimerCompleted,
                    null,
                    null
                    ),
                _gameplayIntervalGeneralParameters);
        }

        public void StopLevelTimer()
        {
            _gameplayIntervalsController.CancelInterval(_currentTimerId);
        }

        public void AffectLevelTimer(short amount)
        {
            _currentTimer = (ushort)Mathf.Max(0, _currentTimer + amount);
        }

        public LevelTimerResult EvaluateTimer()
        {
            var levelResult = new LevelTimerResult(_currentTimer);

            if (IsEasyMode)
            {
                levelResult.NumberOfStars = EasyModeGivesMaxStars ? (byte)_levelStarData.Length : (byte)0;
            }
            else
            {
                foreach (var levelStarData in _levelStarData)
                {
                    if (levelResult.RemainingTime <
                        _gameplayIntervalGeneralParameters.DurationSeconds - levelStarData.TimeLeftMoreThan)
                    {
                        continue;
                    }

                    levelResult.NumberOfStars++;
                }
            }

            StopLevelTimer();

            return levelResult;
        }

        public UniTask Load()
        {
            _levelLoader.LevelBaseSO
                .Skip(1)
                .Subscribe(LevelDataLoadedHandler)
                .AddTo(_disposable);

            _reloadController.AddReloadableObject(this);

            _levelController.IsLevelStarted
                .Skip(1)
                .Subscribe(LevelStartedHandler)
                .AddTo(_disposable);

            Observable
                .EveryValueChanged(_playerProfilesController.Service.CurrentProfile, profile => profile.GameMode)
                .Skip(1)
                .Subscribe(_ => GameModeChangedHandler())
                .AddTo(_disposable);

            _levelController.LevelFinished += LevelFinishedHandler;

            _timerProgressView = _gameplaySceneReferences.TimerProgressView;

            return UniTask.CompletedTask;
        }

        public UniTask Reload()
        {
            _pauseSources.Clear();
            _timerIsPaused = false;

            _gameplayIntervalsController.CancelInterval(_currentTimerId);

            _timerProgressView.ResetTimer();

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            _disposable.Dispose();

            _levelController.LevelFinished -= LevelFinishedHandler;

            _reloadController.RemoveReloadableObject(this);
        }
    }
}