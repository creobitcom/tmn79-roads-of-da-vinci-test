using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles
{
    public class CollectibleObjectHider : MonoBehaviour
    {
        [SerializeField] private CollectibleItemSO _item;

        private ICollectiblesService _collectiblesService;
        private ILevelLoader _levelLoader;

        [Inject]
        private void Construct(ICollectiblesService collectiblesService, ILevelLoader levelLoader)
        {
            _collectiblesService = collectiblesService;
            _levelLoader = levelLoader;

            _levelLoader.LevelLoaded += OnLevelLoaded;
        }

        private void OnDestroy()
        {
            if (_levelLoader != null)
            {
                _levelLoader.LevelLoaded -= OnLevelLoaded;
            }
        }

        private void OnLevelLoaded()
        {
            if (_item == null)
            {
                return;
            }

            _collectiblesService.RegisterLevelCollectible(_item);

            if (!_collectiblesService.IsFullyCollected(_item))
            {
                return;
            }

            var staticObject = GetComponent<StaticObjectView>();

            if (staticObject != null && staticObject.InteractionGraphNode != null)
            {
                staticObject.UnBlockInteractionGraphNode();
            }

            gameObject.SetActive(false);
        }
    }
}
