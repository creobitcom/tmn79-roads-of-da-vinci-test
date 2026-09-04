using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles
{
    public class CollectiblesBridge : MonoBehaviour
    {
        private ICollectiblesService _service;

        [Inject]
        private void Construct(ICollectiblesService service) => _service = service;

        public void GiveArtifact(CollectibleItemSO artifact_object)
        {
            if (artifact_object == null || artifact_object.Type != CollectibleType.Artifact)
            {
                return;
            }
            _service.AddProgress(artifact_object);
        }

        public void GiveTrophy(CollectibleItemSO trophy_object)
        {
            if (trophy_object == null || trophy_object.Type != CollectibleType.Trophy)
            {
                return;
            }
            _service.AddProgress(trophy_object);
        }
    }
}
