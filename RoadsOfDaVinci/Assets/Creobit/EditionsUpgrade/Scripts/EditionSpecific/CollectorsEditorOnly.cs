using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/CollectorsEditorOnly")]
    public class CollectorsEditorOnly : MonoBehaviour
    {
        private void Awake()
        {
#if !COLLECTOR
            gameObject.SetActive(false);
#else
            Destroy(this);
#endif
        }
    }
}