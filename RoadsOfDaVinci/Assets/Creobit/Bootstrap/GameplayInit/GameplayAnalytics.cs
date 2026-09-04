#if TMN_Module
using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Bootstrap.Core.Scripts.Runtime.Analytics;
using R3;
using VContainer.Unity;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Initialization.GameplayInit
{
    public class GameplayAnalytics : IStartable, IDisposable
    {
        private readonly AnalyticsRecorder _analyticsRecorder;
        private readonly ILevelController _levelController;
        private readonly ILevelLoader _levelLoader;
        private readonly ILevelTimer _levelTimer;
        private readonly IReloadController _reloadController;

        private IDisposable _levelStartedSubscription;

        public GameplayAnalytics(AnalyticsRecorder analyticsRecorder,
            ILevelController levelController,
            ILevelLoader levelLoader,
            ILevelTimer levelTimer,
            IReloadController reloadController)
        {
            _analyticsRecorder = analyticsRecorder;
            _levelController = levelController;
            _levelLoader = levelLoader;
            _levelTimer = levelTimer;
            _reloadController = reloadController;
        }

        public void Start()
        {
            _levelStartedSubscription = _levelController.IsLevelStarted
                .Where(isStarted => isStarted)
                .Subscribe(_ => _analyticsRecorder.LevelStarted(GetCurrentLevelNumber()));

            _levelController.LevelFinished += LevelFinishedHandler;
            _reloadController.ReloadRequested += ReloadRequestedHandler;
        }

        public void Dispose()
        {
            _levelStartedSubscription?.Dispose();
            _levelController.LevelFinished -= LevelFinishedHandler;
            _reloadController.ReloadRequested -= ReloadRequestedHandler;
        }

        private void LevelFinishedHandler(LevelBaseSO levelBase)
        {
            _analyticsRecorder.LevelCompleted(levelBase.LevelNumber, _levelTimer.EvaluateTimer().NumberOfStars);
        }

        private void ReloadRequestedHandler()
        {
            _analyticsRecorder.LevelRestarted(GetCurrentLevelNumber());
        }

        private int GetCurrentLevelNumber()
        {
            var levelBase = _levelLoader.LevelBaseSO.CurrentValue;
            return levelBase != null ? levelBase.LevelNumber : 0;
        }
    }
}
#endif
