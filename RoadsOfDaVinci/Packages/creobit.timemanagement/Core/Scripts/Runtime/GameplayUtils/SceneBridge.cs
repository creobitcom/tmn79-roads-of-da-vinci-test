using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Controller;
using R3;
using UltEvents;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils
{
    public class SceneBridge : MonoBehaviour
    {
        private GameplaySceneReferences _gameplaySceneReferences;
        private ITooltipController _tooltipController;
        private ILevelController _levelController;
        private ILoadingController _loadingController;

        [SerializeField]
        private UltEvent _onLevelStarted;
        
        [SerializeField]
        private UltEvent _onGameLoaded;

        [Inject]
        private void Construct(GameplaySceneReferences gameplaySceneReferences,
            ILevelController levelController,
            ILoadingController loadingController)
        {
            _gameplaySceneReferences = gameplaySceneReferences;
            _levelController = levelController;
            _loadingController = loadingController;

            _levelController.IsLevelStarted
                .Subscribe(OnLevelStartedHandler)
                .AddTo(this);

            _loadingController.GameLoaded += GameLoadedHandler;
        }

        public void SetGlobalLightState(float intensity)
        {
            _gameplaySceneReferences.GlobalLight.intensity = intensity;
        }

        public void SetActiveGlobalDarkVolume(bool active)
        {
            _gameplaySceneReferences.GlobalDarkVolume.gameObject.SetActive(active);
        }

        public void ShowFinishScreen()
        {
            _levelController.ShowFinishScreen();
        }

        public void ShowPressAnythingToContinueState(bool active)
        {
            _levelController.SetPressAnythingToContinueObjectState(active);
        }

        public void StartLevel()
        {
            _levelController.StartLevel();
        }

        private void GameLoadedHandler()
        {
            _onGameLoaded?.Invoke();
        }

        private void OnLevelStartedHandler(bool levelStarted)
        {
            if (!levelStarted)
            {
                return;
            }
            
            _onLevelStarted?.Invoke();
        }

        private void OnDestroy()
        {
            _loadingController.GameLoaded -= GameLoadedHandler;
        }
    }
}