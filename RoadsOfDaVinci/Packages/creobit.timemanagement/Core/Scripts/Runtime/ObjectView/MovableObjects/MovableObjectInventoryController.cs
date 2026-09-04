using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects
{
    public class MovableObjectInventoryController
    {
        private readonly IGameResourcesSystem _gameResourcesSystem;
        private readonly GameplaySceneReferences _gameplaySceneReferences;

        public MovableObjectInventoryController(IGameResourcesSystem gameResourcesSystem, 
            GameplaySceneReferences gameplaySceneReferences)
        {
            _gameResourcesSystem = gameResourcesSystem;
            _gameplaySceneReferences = gameplaySceneReferences;
        }

        public void Dispose()
        {
            _gameResourcesSystem?.Dispose();
        }

        public void StashResources(MovableObjectView unit)
        {
            List<ResourceAmount> resourcesAmounts = new();

            foreach (var resource in unit.UnitInventory)
            {
                _gameResourcesSystem.AddResource(resource.Key, resource.Value);
                resourcesAmounts.Add(new ResourceAmount(resource.Key, resource.Value));
            }

            _gameplaySceneReferences.ResourceAmountAdded
                .ShowResourcesAmounts(resourcesAmounts.ToArray(),
                    _gameplaySceneReferences.ResourcesView, unit.CurrentBasement? unit.CurrentBasement.transform : unit.transform, Vector3.up)
                .Forget();

            ClearInventory(unit);
        }

        public void ClearInventory(MovableObjectView unit)
        {
            unit.resourceIcon.gameObject.SetActive(false);

            unit.UnitInventory.Clear();
        }
        
        private void AddResource(MovableObjectView unit, ResourceAmount resourceAmount)
        {
            unit.resourceIcon.sprite = resourceAmount.Resource.Image;

            // Особые предметы раньше не показывались над юнитом никогда. Теперь это решает
            // сам ассет: showCarryIcon выключен по умолчанию, поэтому проекты на merge-логике
            // (GG10, LA7) ведут себя ровно как прежде, а LA8 включает галку точечно.
            unit.resourceIcon.gameObject.SetActive(
                resourceAmount.Resource is not InventoryResource inventoryResource
                || inventoryResource.showCarryIcon);

            if (!unit.UnitInventory.ContainsKey(resourceAmount.Resource))
            {
                unit.UnitInventory.Add(resourceAmount.Resource, resourceAmount.Amount);

                return;
            }

            unit.UnitInventory[resourceAmount.Resource] += resourceAmount.Amount;
        }

        private bool HasResource(MovableObjectView unit, ResourceBaseSO resource)
        {
            return unit.UnitInventory.ContainsKey(resource);
        }

        public void AddResources(MovableObjectView unit, ResourceAmount[] resources)
        {
            foreach (var resource in resources)
            {
                AddResource(unit, resource);
            }
        }

        public bool HasResources(MovableObjectView unit, ResourceBaseSO[] resources)
        {
            var containAll = true;

            foreach (var resource in resources)
            {
                if (!HasResource(unit, resource))
                {
                    containAll = false;

                    break;
                }
            }

            return containAll;
        }
    }
}