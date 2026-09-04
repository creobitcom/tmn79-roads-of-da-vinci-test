using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Comics;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using Creobit.Bootstrap.Core.Scripts.Runtime.DTO;
using Creobit.Bootstrap.Core.Scripts.Runtime.Meta;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using UltEvents;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Creobit.Bootstrap.Core.Scripts.Runtime
{
    public class LoadingBridge : MonoBehaviour
    {
        private RuntimeData _runtimeData;
        private ILoadingController _loadingController;
        private IObjectResolver _objectResolver;

        [SerializeField] private UltEvent _onSceneLoaded;
        [SerializeField] private AllLevelsSO _allLevels;
        [SerializeField] private List<ComicsData> _comicsData;

        [Inject]
        private void Construct(ILoadingController loadingController,
            RuntimeData runtimeData,
            IObjectResolver objectResolver)
        {
            _runtimeData = runtimeData;
            _loadingController = loadingController;
            _objectResolver = objectResolver;
            _loadingController.LoadingFinished += OnSceneLoaded;
        }

        public void LoadGameplayScene()
        {
            _loadingController.ScheduleSceneLoading(RuntimeConstants.SceneIndex.GameplayScene);
            _ = _loadingController.LoadScheduledScene();
        }

        public void LoadMenu()
        {
            _runtimeData.MetaState = MetaStates.Menu;
            _loadingController.ScheduleSceneLoading(RuntimeConstants.SceneIndex.MetaScene);
            _ = _loadingController.LoadScheduledScene();
        }

        public void LoadMap()
        {
            _runtimeData.MetaState = MetaStates.Map;
            _loadingController.ScheduleSceneLoading(RuntimeConstants.SceneIndex.MetaScene);
            _ = _loadingController.LoadScheduledScene();
        }

        public void NextOrMap()
        {
            if (_objectResolver.TryResolve<ILevelLoader>(out var levelLoader)
                && levelLoader.LevelBaseSO.CurrentValue != null)
            {
                var current = levelLoader.LevelBaseSO.CurrentValue;

                var isLastLevel = _allLevels != null
                    && _allLevels.AllLevels != null
                    && current.LevelNumber >= _allLevels.AllLevels.Count;

                var reachedLimit = _allLevels != null
                    && _allLevels.LimitLevels
                    && current.LevelNumber >= _allLevels.MaxLevel;

#if COLLECTOR
                var reachedMainCampaignEnd = false;
#else
                var reachedMainCampaignEnd = _allLevels != null
                    && _allLevels.MainCampaignLevels > 0
                    && current.LevelNumber >= _allLevels.MainCampaignLevels;
#endif

#if !PREMIUM
                if (!isLastLevel && !reachedLimit && !reachedMainCampaignEnd)
                {
                    var nextLevelNumber = current.LevelNumber + 1;

                    if (!Creobit.EditionsUpgrade.UnlockLevelManager.IsLevelPlayableWithoutPurchase(nextLevelNumber))
                    {
                        LoadMap();
                        return;
                    }

                    Creobit.EditionsUpgrade.UnlockLevelManager.RaiseUnlockProgress(nextLevelNumber);
                }
#endif

                var hasPendingComic = false;
                if (_comicsData != null && _comicsData.Count > 0)
                {
                    _objectResolver.TryResolve<ISaveController>(out var saveController);

                    foreach (var comic in _comicsData)
                    {
                        if (comic == null) continue;

                        var isPassed = saveController != null && saveController.Service != null
                            && saveController.Service.IsComicsPassed(comic.ComicsName);

                        if (isPassed) continue;

                        if (comic.ComicsCondition == ComicsConditions.AfterLevel && comic.LevelToShow == current.LevelNumber)
                        {
                            hasPendingComic = true;
                            break;
                        }

                        if (comic.ComicsCondition == ComicsConditions.BeforeLevel && comic.LevelToShow == current.LevelNumber + 1)
                        {
                            hasPendingComic = true;
                            break;
                        }
                    }
                }

                if (!isLastLevel && !reachedLimit && !reachedMainCampaignEnd && !hasPendingComic && current.NextLevel.RuntimeKeyIsValid())
                {
                    levelLoader.LoadNextLevel();
                    return;
                }

                if (!isLastLevel && !reachedLimit && !reachedMainCampaignEnd)
                {
                    _runtimeData.AutoStartNextLevel = true;
                }
            }

            LoadMap();
        }

        public void loadTMNSelect()
        {
            _loadingController.ScheduleSceneLoading(RuntimeConstants.SceneIndex.GameplayScene);
            _ = _loadingController.LoadScheduledScene();
        }
        
        private void OnDestroy()
        {
            _loadingController.LoadingFinished -= OnSceneLoaded;
        }

        private void OnSceneLoaded()
        {
            _onSceneLoaded?.Invoke();
        }
    }
}