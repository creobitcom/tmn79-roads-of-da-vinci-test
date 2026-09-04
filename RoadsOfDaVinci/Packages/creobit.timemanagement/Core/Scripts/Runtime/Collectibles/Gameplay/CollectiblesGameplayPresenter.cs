using System;
using Creobit.Loading;
using _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles
{
    public class CollectiblesGameplayPresenter : ILoadUnit, IDisposable, IReloadable
    {
        private readonly ICollectiblesService _collectiblesService;
        private readonly ICutsceneController _cutsceneController;
        private readonly IReloadController _reloadController;
        private readonly ILevelLoader _levelLoader;

        [Inject]
        public CollectiblesGameplayPresenter(
            ICollectiblesService collectiblesService,
            ICutsceneController cutsceneController,
            IReloadController reloadController,
            ILevelLoader levelLoader)
        {
            _collectiblesService = collectiblesService;
            _cutsceneController = cutsceneController;
            _reloadController = reloadController;
            _levelLoader = levelLoader;
        }

        public UniTask Load()
        {
            _collectiblesService.OnItemFullyCollected += OnItemCollected;
            _levelLoader.BeforeLevelLoaded += OnBeforeLevelLoaded;
            _reloadController.AddReloadableObject(this);
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            _collectiblesService.OnItemFullyCollected -= OnItemCollected;
            _levelLoader.BeforeLevelLoaded -= OnBeforeLevelLoaded;
            _reloadController.RemoveReloadableObject(this);
            _collectiblesService.ClearLevelCollectible();
        }

        public UniTask Reload() => UniTask.CompletedTask;

        private void OnBeforeLevelLoaded()
        {
            _collectiblesService.ClearLevelCollectible();
        }

        private void OnItemCollected(CollectibleItemSO item)
        {
            if (item.ComicOnCollect != null)
            {
                _cutsceneController.PlayCutsceneAsync(item.ComicOnCollect).Forget();
            }

        }
    }
}
