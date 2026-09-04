using System;
using System.Collections.Generic;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles
{
    public class CollectiblesService : ICollectiblesService
    {
        private readonly IPlayerProfilesController _profilesController;
        private readonly ISaveController _saveController;

        public event Action<CollectibleItemSO, int> OnItemProgressChanged;
        public event Action<CollectibleItemSO> OnItemFullyCollected;

        public CollectiblesService(IPlayerProfilesController profilesController, ISaveController saveController)
        {
            _profilesController = profilesController;
            _saveController = saveController;
        }

        public CollectibleItemSO LevelCollectible { get; private set; }
        public bool LevelCollectibleCollectedOnLevelStart { get; private set; }

        public UniTask Load() => UniTask.CompletedTask;

        public void RegisterLevelCollectible(CollectibleItemSO item)
        {
            if (item == null) return;

            LevelCollectible = item;
            LevelCollectibleCollectedOnLevelStart = IsFullyCollected(item);
        }

        public void ClearLevelCollectible()
        {
            LevelCollectible = null;
            LevelCollectibleCollectedOnLevelStart = false;
        }

        public void AddProgress(CollectibleItemSO item, int amount = 1)
        {
            if (item == null) return;
            var profile = _profilesController.Service.CurrentProfile;
            
            if (profile.CollectiblesProgress == null) profile.CollectiblesProgress = new Dictionary<string, int>();

            int currentProgress = profile.CollectiblesProgress.GetValueOrDefault(item.Id, 0);
            if (currentProgress >= item.MaxParts)
            {
                Debug.LogError($"[CollectiblesService] ПРЕДМЕТ {item.Id} УЖЕ СОБРАН!.");
                return;
            }

            int newProgress = currentProgress + amount;
            profile.CollectiblesProgress[item.Id] = newProgress;

            Debug.LogError($"[CollectiblesService] СОХРАНЕНО в ПРОФИЛЬ: {item.Type} '{item.Id}' прогресс: {newProgress}/{item.MaxParts}");
            OnItemProgressChanged?.Invoke(item, newProgress);

            if (newProgress >= item.MaxParts)
            {
                Debug.LogError($"[CollectiblesService] 🔥 {item.Type.ToString().ToUpper()} '{item.Id}' СОБРАН ПОЛНОСТЬЮ! 🔥");
                OnItemFullyCollected?.Invoke(item);
            }
        }

        public int GetProgress(string itemId)
        {
            return _profilesController.Service.CurrentProfile.CollectiblesProgress?.GetValueOrDefault(itemId, 0) ?? 0;
        }

        public bool IsFullyCollected(CollectibleItemSO item) => GetProgress(item.Id) >= item.MaxParts;
    }
}
