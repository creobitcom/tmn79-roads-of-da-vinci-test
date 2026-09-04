using System;
using Creobit.Loading;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles
{
    public interface ICollectiblesService : ILoadUnit
    {
        event Action<CollectibleItemSO, int> OnItemProgressChanged;
        event Action<CollectibleItemSO> OnItemFullyCollected;

        CollectibleItemSO LevelCollectible { get; }
        bool LevelCollectibleCollectedOnLevelStart { get; }

        void AddProgress(CollectibleItemSO item, int amount = 1);
        int GetProgress(string itemId);
        bool IsFullyCollected(CollectibleItemSO item);
        void RegisterLevelCollectible(CollectibleItemSO item);
        void ClearLevelCollectible();
    }
}
