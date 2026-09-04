using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/StandardEditorOnly")]
    public class StandardEditorOnly : MonoBehaviour
    {
#if PREMIUM
        private void Awake()
        {
        gameObject.SetActive(false);
        }
#endif 
    }
}