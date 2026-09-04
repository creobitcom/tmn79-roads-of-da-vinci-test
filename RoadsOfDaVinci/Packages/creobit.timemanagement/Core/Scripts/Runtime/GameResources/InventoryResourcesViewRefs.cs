using System.Collections.Generic;
using UltEvents;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameResources
{
    public class InventoryResourcesViewRefs : MonoBehaviour
    {
        [SerializeField]
        protected ResourceViewRefs prefab;

        [SerializeField]
        public UltEvent OnInventoryShow;

        [SerializeField]
        public UltEvent OnInventoryHide;

        [SerializeField]
        protected Transform resourcesParent;

        public List<ResourceViewRefs> resourceTextImages = new();

        private List<ResourceViewRefs> disabledRefs = new();

        /// <summary>
        /// Отдаёт слот под ресурс: переиспользует отключённый из пула либо создаёт новый.
        /// Слот встаёт последним ребёнком — порядок на экране задаёт раскладка родителя.
        /// </summary>
        public virtual ResourceViewRefs GetResourceView()
        {
            ResourceViewRefs pair;

            if (disabledRefs.Count > 0)
            {
                pair = disabledRefs[0];
                disabledRefs.RemoveAt(0);
                pair.isBlocked = false;
                pair.spriteRect.localPosition = Vector2.zero;
                pair.gameObject.SetActive(true);
                pair.transform.SetAsLastSibling();
            }
            else
            {
                pair = Instantiate(prefab, resourcesParent);
            }

            resourceTextImages.Add(pair);

            return pair;
        }

        public virtual void RemoveResource(ResourceViewRefs resourceView)
        {
            resourceTextImages.Remove(resourceView);
            disabledRefs.Add(resourceView);
            resourceView.gameObject.SetActive(false);
        }

        /// <summary>
        /// Наполняет слот данными ресурса. Базовое поведение: число видно только когда предметов больше одного.
        /// Проект может переопределить (LA8: SpecialItemsPanelView показывает число всегда и анимирует слот).
        /// </summary>
        public virtual void SetResource(ResourceViewRefs resourceRefs, InventoryResource inventoryResource, int amount)
        {
            resourceRefs.resource = inventoryResource;
            resourceRefs.image.sprite = inventoryResource.Image;
            resourceRefs.countText.text = amount > 1 ? amount.ToString() : string.Empty;
            resourceRefs?.amountObject?.SetActive(amount > 1);
        }
    }
}
