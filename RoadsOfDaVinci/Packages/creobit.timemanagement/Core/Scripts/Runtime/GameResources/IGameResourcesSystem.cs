using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Loading;
using ObservableCollections;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameResources
{
    public interface IGameResourcesSystem : ILoadUnit, IReloadable, IDisposable
    {
        public int GetResourceAmount(ResourceBaseSO resource);
        
        /// <summary>
        /// Checks if the resource is enough.
        /// </summary>
        /// <param name="resourceAmounts">The array of pairs (ResourceSO/count of this resource) to check.</param>
        /// <returns>Is resource enough.</returns>
        public bool IsEnoughResources(ResourceAmount[] resourceAmounts);
        
        /// <summary>
        /// Add resources.
        /// </summary>
        /// <param name="resourcesAmount">The array of pairs (ResourceSO/count of this resource) to add.</param>
        public void AddResource(ResourceAmount[] resourcesAmount);
        
        /// <summary>
        /// Add resource.
        /// </summary>
        /// <param name="resourceAmount">The pair (ResourceSO/count of this resource) to add.</param>
        public void AddResource(ResourceAmount resourceAmount);
        
        /// <summary>
        /// Add resource.
        /// </summary>
        /// <param name="resource">The resource to add.</param>
        /// <param name="amount">Count of resource to add.</param>
        public void AddResource(ResourceBaseSO resource, int amount);
        
        /// <summary>
        /// Subtract resource.
        /// </summary>
        /// <param name="resourceAmount">The pair (ResourceSO/count of this resource) to subtract.</param>
        public void SubtractResource(ResourceAmount resourceAmount);
        
        /// <summary>
        /// Subtract resources.
        /// </summary>
        /// <param name="resourcesAmount">The array of pairs (ResourceSO/count of this resource) to subtract.</param>
        public void SubtractResource(ResourceAmount[] resourcesAmount);
        
        /// <summary>
        /// Check if resources contains resource key. If not - add this resource
        /// </summary>
        /// <param name="resource">Resource to check.</param>
        public void CheckResourceIsLoaded(ResourceBaseSO resource);

        public IReadOnlyObservableDictionary<ResourceBaseSO, int> Resources { get; }
        public IReadOnlyObservableDictionary<ResourceBaseSO, int> InventoryResources { get; }
        public List<WorkerResourceByTag> workerResource { get; set; }
    }
}