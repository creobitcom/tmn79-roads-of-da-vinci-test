using System.Collections.Generic;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using ObservableCollections;
using R3;
using VContainer;
using Log = Creobit.Logger.Log;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameResources
{
    public class GameResourcesSystem : IGameResourcesSystem
    {
        private readonly CompositeDisposable _disposable = new();
        private readonly ObservableDictionary<ResourceBaseSO, int> _resources = new ();
        private readonly ObservableDictionary<ResourceBaseSO, int> _inventoryResources = new ();
        
        private ILevelLoader _levelLoader;
        private GameplaySceneReferences _gameplaySceneReferences;
        public List<WorkerResourceByTag> workerResource { get; set; }
        
        public IReadOnlyObservableDictionary<ResourceBaseSO, int> Resources => _resources;
        public IReadOnlyObservableDictionary<ResourceBaseSO, int> InventoryResources => _inventoryResources;

        [Inject]
        private void Construct(ILevelLoader levelController,
            GameplaySceneReferences gameplaySceneReferences)
        {
            _levelLoader = levelController;
            _gameplaySceneReferences = gameplaySceneReferences;
            workerResource = gameplaySceneReferences.WorkerResource;
        }
        
        public UniTask Load()
        {
            _levelLoader.LevelBaseSO
                .Skip(1)
                .Subscribe(InitializeStartResources)
                .AddTo(_disposable);

            return UniTask.CompletedTask;
        }

        private void InitializeStartResources(LevelBaseSO currentLevelDataSO)
        {
            _resources.Clear();
            _inventoryResources.Clear();
            
            foreach (var resourceAmount in currentLevelDataSO.StartResources)
            {
                if (resourceAmount.Resource is InventoryResource)
                {
                    _inventoryResources.Add(resourceAmount.Resource, resourceAmount.Amount);
                }
                else
                {
                    _resources.Add(resourceAmount.Resource, resourceAmount.Amount);
                }
            }

            if (currentLevelDataSO.ResourcesForResourcePanel != null)
            {
                InitializeResourceView(currentLevelDataSO);
            }
            
            UpdateResourcesView();
            UpdateInventoryResourcesView();
        }

        private void InitializeResourceView(LevelBaseSO currentLevelDataSO)
        {
            for (var i = 0; i < currentLevelDataSO.ResourcesForResourcePanel.Count; i++)
            {
                var resource = currentLevelDataSO.ResourcesForResourcePanel[i];
                _gameplaySceneReferences.ResourcesView.resources[i].resource = resource;
                _gameplaySceneReferences.ResourcesView.resources[i].image.sprite = resource.PanelResourcesSprite;
            }
        }

        private void UpdateResourcesView()
        {
            if (_resources is null)
                return;

            foreach (var viewResource in _gameplaySceneReferences.ResourcesView.resources)
            {
                var resourceCount = 0;

                foreach (var resource in _resources)
                {
                    if (resource.Key.Name == viewResource.resource.Name)
                    {
                        resourceCount = resource.Value;
                    }
                }

                viewResource.countText.text = resourceCount.ToString();
            }
        }
        
        private void UpdateInventoryResourcesView()
        {
            if (_inventoryResources is null)
                return;
            
            var inventoryView = _gameplaySceneReferences.InventoryResourcesViewRefs;

            for (var index = inventoryView.resourceTextImages.Count - 1; index >= 0; index--)
            {
                var pair = inventoryView.resourceTextImages[index];
                var useless = true;
                
                foreach (var resource in _inventoryResources)
                {
                    if (pair.resource.Name == resource.Key.Name && resource.Value != 0)
                    {
                        useless = false;
                        break;
                    }
                }

                if (useless)
                {
                    inventoryView.RemoveResource(pair);
                }
                
            }

            foreach (var resource in _inventoryResources)
            {
                if (resource.Value == 0)
                    continue;
                
                ResourceViewRefs resourcePair = null;
                
                foreach (var pair in inventoryView.resourceTextImages)
                {
                    if (pair.resource.Name == resource.Key.Name)
                    {
                        resourcePair = pair;
                        break;
                    }
                }

                if (resourcePair == null)
                {
                    resourcePair = inventoryView.GetResourceView();
                }

                inventoryView.SetResource(resourcePair, resource.Key as InventoryResource, resource.Value);
            }

            for (var index = inventoryView.resourceTextImages.Count - 1; index >= 0; index--)
            {
                var inventoryResource = inventoryView.resourceTextImages[index];
                var resource = ((InventoryResource)inventoryResource.resource);
                if (!inventoryResource.isBlocked
                    && resource.isMerge
                    && _inventoryResources.ContainsKey(resource.mergeWith))
                {
                    var mergeResource = inventoryView.resourceTextImages
                        .First(rv => rv.resource == resource.mergeWith);

                    MergeInventoryResourcesStart(inventoryResource, mergeResource).Forget();
                }
            }

            if (_inventoryResources.Count(resource => resource.Value > 0) > 0)
            {
                inventoryView.OnInventoryShow?.Invoke();
            }
            else
            {
                inventoryView.OnInventoryHide?.Invoke();
            }
        }

        private async UniTaskVoid MergeInventoryResourcesStart(ResourceViewRefs first, ResourceViewRefs second)
        {
            first.isBlocked = true;
            second.isBlocked = true;

            await UniTask.DelayFrame(1);

            var target = first.spriteRect.position + 
                         (second.spriteRect.position - first.spriteRect.position) / 2; // TODO: many animations
            
            first
                .spriteRect
                .DOMove(target, 1);
            
            second
                .spriteRect
                .DOMove(target, 1)
                .OnComplete(() => MergeInventoryResourcesEnd(first, second)); 
        }
        
        private void MergeInventoryResourcesEnd(ResourceViewRefs first, ResourceViewRefs second)
        {
            var inventoryView = _gameplaySceneReferences.InventoryResourcesViewRefs;
            var result = ((InventoryResource)first.resource).mergedResource;
            
            AddResource(result, 1);
            SubtractResource(new ResourceAmount(first.resource, 1));
            SubtractResource(new ResourceAmount(second.resource, 1));
            
            UpdateInventoryResourcesView();
            
            inventoryView.resourceTextImages.First(resourceView => resourceView.resource == result).onMerged?.Invoke();
        }

        private bool IsEnoughResource(ResourceAmount resourceAmount)
        {
            return GetResourceAmount(resourceAmount.Resource) >= resourceAmount.Amount;
        }

        public UniTask Reload()
        {
            InitializeStartResources(_levelLoader.LevelBaseSO.CurrentValue);
            
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            _disposable.Dispose();
        }

        public int GetResourceAmount(ResourceBaseSO resource)
        {
            var resources = resource is InventoryResource 
                ? _inventoryResources
                : _resources;
            
            _ = resources.TryGetValue(resource, out var result);

            return result;
        }

        public bool IsEnoughResources(ResourceAmount[] resourceAmounts)
        {
            foreach (var resourceAmount in resourceAmounts)
            {
                var isEnough = IsEnoughResource(resourceAmount);

                if (!isEnough)
                {
                    return false;
                }
            }

            return true;
        }

        public void AddResource(ResourceAmount[] resourcesAmount)
        {
            foreach (var resourceAmount in resourcesAmount) 
                AddResource(resourceAmount);
        }

        public void AddResource(ResourceAmount resourceAmount)
        {
            SetResourceAmount(GetResourceAmount(resourceAmount.Resource) + resourceAmount.Amount,
                resourceAmount.Resource);
        }

        public void AddResource(ResourceBaseSO resource, int amount)
        {
            SetResourceAmount(GetResourceAmount(resource) + amount, resource);
        }

        public void SubtractResource(ResourceAmount resourceAmount)
        {
            if (!IsEnoughResource(resourceAmount))
            {
                return;
            }

            SetResourceAmount(GetResourceAmount(resourceAmount.Resource) - resourceAmount.Amount,
                resourceAmount.Resource);
        }

        public void SubtractResource(ResourceAmount[] resourcesAmount)
        {
            if (!IsEnoughResources(resourcesAmount))
            {
                return;
            }

            foreach (var resourceAmount in resourcesAmount)
                SubtractResource(resourceAmount);
        }

        private void SetResourceAmount(int amount, ResourceBaseSO resource)
        {
            var resources = resource is InventoryResource 
                ? _inventoryResources
                : _resources;
            
            _ = resources.TryAdd(resource, 0);

            if (amount < 0)
            {
                Log.Gameplay.Error($"Failed to set resource amount. (amount: {amount}, resource: {resource})");
                amount = 0;
            }

            resources[resource] = amount;
            UpdateResourcesView();
            UpdateInventoryResourcesView();
        }

        public void CheckResourceIsLoaded(ResourceBaseSO resource)
        {
            var resources = resource is InventoryResource 
                ? _inventoryResources
                : _resources;
            
            resources.TryAdd(resource, 0);
        }
    }
}