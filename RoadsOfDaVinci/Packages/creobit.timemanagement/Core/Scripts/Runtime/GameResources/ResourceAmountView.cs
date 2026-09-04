using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameResources
{
    public class ResourceAmountView : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private TextMeshProUGUI _amount;
        [SerializeField] private string _amountPrefix;

        private void Initialize(ResourceAmount resourceAmount, ResourcesViewRefs resourcesView)
        {
            Sprite sprite = null;

            foreach (var resourceTextPair in resourcesView.resources)
            {
                if(resourceTextPair.resource.Name == resourceAmount.Resource.Name)
                {
                    sprite = resourceTextPair.image.sprite;

                    break;
                }
            }

            // Особые ресурсы (InventoryResource) в панели базовых ресурсов не лежат,
            // поэтому для них спрайт берём напрямую из самого ресурса.
            _image.sprite = sprite != null ? sprite : resourceAmount.Resource.Image;

            _amount.text = _amountPrefix + resourceAmount.Amount;
        }

        private void ShowResourceAmount(ResourceAmount resourceAmount, ResourcesViewRefs resourcesView, Transform parent, Vector3 offset) 
        {
            var resourceAmountView = Instantiate(gameObject, parent).GetComponent<ResourceAmountView>();
            resourceAmountView.transform.position += offset;
            resourceAmountView.Initialize(resourceAmount, resourcesView);
        }

        public async UniTask ShowResourcesAmounts(ResourceAmount[] resourcesAmounts, ResourcesViewRefs resourcesView, 
            Transform parent, Vector3 offset)
        {
            foreach (var resourceAmount in resourcesAmounts)
            {
                ShowResourceAmount(resourceAmount, resourcesView, parent, offset);
                await UniTask.Delay(RuntimeConstants.Delays.ResourceAmountSpawn);
            }
        }

        public void Destroy()
        {
            Destroy(gameObject);
        }
    }
}
