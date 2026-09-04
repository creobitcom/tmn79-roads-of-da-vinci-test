using System;
using System.Collections.Generic;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Localization;
using Creobit.UI;
using Creobit.UI.Utility;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Loader
{
    public class WinBridge : MonoBehaviour, IReloadable
    {
        [SerializeField]
        private GameObject finishEffectsRoot;

        private IUIController _uiController;
        private GameplaySceneReferences _gameplaySceneReferences;
        private ILevelTimer _levelTimer;
        private ILevelController _levelController;
        private IGameResourcesSystem _resourcesSystem;
        private IGameplayInputSystem _gameplayInput;
        private IAchievementsController _achievementsController;
        private IReloadController _reloadController;
        private ICollectiblesService _collectiblesService;

        private DOTweenAnimation[] _winShowAnimations;
        private GameObject _winPanelObject;
        private IDisposable _pauseViewSubscription;

        [Inject]
        private void Construct(IObjectResolver resolver,
            IUIController uiController,
            GameplaySceneReferences gameplaySceneReferences,
            IGameResourcesSystem resourcesSystem,
            IGameplayInputSystem gameplayInputSystem,
            ILevelTimer levelTimer,
            ILevelController levelController,
            IAchievementsController achievementsController,
            IReloadController reloadController)
        {
            _uiController = uiController;
            _gameplaySceneReferences = gameplaySceneReferences;
            _resourcesSystem = resourcesSystem;
            _gameplayInput = gameplayInputSystem;
            _levelTimer = levelTimer;
            _levelController = levelController;
            _achievementsController = achievementsController;
            _reloadController = reloadController;

            resolver.TryResolve(out _collectiblesService);

            _reloadController.AddReloadableObject(this);
        }

        private void OnDestroy()
        {
            _pauseViewSubscription?.Dispose();
            _reloadController?.RemoveReloadableObject(this);
        }

        public UniTask Reload()
        {
            CancelPendingWinView();

            return UniTask.CompletedTask;
        }

        public async void ShowWinView()
        {
            _levelController.NotifyWinViewShowing();
            _gameplayInput.Disable();

            _uiController.ShowPanel(_gameplaySceneReferences.WinView,
                _gameplaySceneReferences.GameplayCanvasLayers[3].transform);

            var allResourceCount = _resourcesSystem.Resources.Values.Sum();
            var winPanel = await _uiController.GetPanel(_gameplaySceneReferences.WinView);

            if (winPanel == null)
            {
                return;
            }

            TrackWinViewShowAnimations(winPanel.gameObject);

            var winView = winPanel.GetComponent<WinViewRefs>();

            if (winView != null && winView.achievementsParent != null) 
            {
                foreach (Transform child in winView.achievementsParent) 
                {
                    Destroy(child.gameObject);
                }
            }
            
            var controller = _achievementsController as AchievementsController;
            
            if (controller?.EarnedThisSession != null && winView.achievementIconPrefab != null && winView.achievementsParent != null)
            {
                foreach (var ach in  controller.EarnedThisSession)
                {
                    if (ach.AchievementData.AchievementIcon != null)
                    {
                        var iconGo = Instantiate(winView.achievementIconPrefab, winView.achievementsParent);
                        var iconView = iconGo.GetComponent<Image>();
                        iconView.sprite = ach.AchievementData.AchievementIcon;
                    }
                }
            }

            foreach (var slider in winView.resourceSliders)
            {
                SetSliderValue(slider,
                    allResourceCount,
                    _resourcesSystem.Resources.GetValueOrDefault(slider.resource, 0));
            }

            winView.starsView.SetStars(_levelTimer.EvaluateTimer());

            UpdateTrophyBlock(winView);

            _levelController.FinishLevel();
        }
        
        private void UpdateTrophyBlock(WinViewRefs winView)
        {
            if (winView.trophyRoot == null || _collectiblesService == null)
            {
                return;
            }

            var item = _collectiblesService.LevelCollectible;
            var showTrophy = item != null
                && item.Type == CollectibleType.Trophy
                && !_collectiblesService.LevelCollectibleCollectedOnLevelStart;

            winView.trophyRoot.SetActive(showTrophy);

            if (winView.starsTransform != null)
            {
                winView.starsTransform.anchoredPosition = showTrophy
                    ? winView.starsPositionWithTrophy
                    : winView.starsPositionDefault;
            }

            if (!showTrophy)
            {
                return;
            }

            var isFound = _collectiblesService.IsFullyCollected(item);

            if (winView.trophyTitleText != null)
            {
                winView.trophyTitleText.text = LocalizationService.Instance.GetText(
                    isFound ? winView.trophyFoundKey : winView.trophyNotFoundKey);
            }

            if (winView.trophyNameText != null)
            {
                winView.trophyNameText.text = LocalizationService.Instance.GetText(item.NameLocalizeKey);
            }

            if (winView.trophyIconImage != null)
            {
                winView.trophyIconImage.sprite = item.Icon;
                winView.trophyIconImage.material = isFound ? null : winView.trophyGrayscaleMaterial;
            }
        }

        private void SetSliderValue(WinResourceSliderRefs slider, int commonCount, int haveCount)
        {
            var percent = (float)haveCount / commonCount;
            slider.slider.value = percent;
            slider.commonCountText.text = commonCount.ToString();
            slider.haveCountText.text = haveCount.ToString();
        }

        private void TrackWinViewShowAnimations(GameObject winPanelObject)
        {
            _winPanelObject = winPanelObject;
            _winShowAnimations = _uiController.GetPanelAnimations(_gameplaySceneReferences.WinView, PanelState.Show);

            HoldWinViewWhilePaused().Forget();
        }

        private async UniTaskVoid HoldWinViewWhilePaused()
        {
            _pauseViewSubscription?.Dispose();
            _pauseViewSubscription = null;

            if (_gameplaySceneReferences.PauseView == null)
            {
                return;
            }

            var pausePanel = await _uiController.GetPanel(_gameplaySceneReferences.PauseView);

            if (pausePanel == null || _winShowAnimations == null)
            {
                return;
            }

            _pauseViewSubscription = Observable
                .EveryValueChanged(pausePanel.gameObject, panelObject => panelObject.activeInHierarchy)
                .Subscribe(SetShowAnimationsPaused)
                .AddTo(this);
        }

        private void SetShowAnimationsPaused(bool isPaused)
        {
            if (_winShowAnimations == null)
            {
                return;
            }

            foreach (var animation in _winShowAnimations)
            {
                var tween = animation == null ? null : animation.tween;

                if (tween == null || !tween.IsActive() || tween.IsComplete())
                {
                    continue;
                }

                if (isPaused)
                {
                    tween.Pause();
                }
                else
                {
                    tween.Play();
                }
            }
        }

        private void CancelPendingWinView()
        {
            _pauseViewSubscription?.Dispose();
            _pauseViewSubscription = null;

            if (_winShowAnimations != null)
            {
                foreach (var animation in _winShowAnimations)
                {
                    var tween = animation == null ? null : animation.tween;

                    if (tween == null || !tween.IsActive() || tween.IsComplete())
                    {
                        continue;
                    }

                    tween.Complete();
                }

                _winShowAnimations = null;
            }

            if (_winPanelObject != null)
            {
                _winPanelObject.SetActive(false);
            }

            _winPanelObject = null;

            StopFinishEffects();
        }

        private void StopFinishEffects()
        {
            if (finishEffectsRoot == null)
            {
                return;
            }

            foreach (var effect in finishEffectsRoot.GetComponentsInChildren<ParticleSystem>(true))
            {
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}