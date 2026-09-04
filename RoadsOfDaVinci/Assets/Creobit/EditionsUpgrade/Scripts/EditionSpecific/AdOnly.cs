using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/AdOnly")]
    public class AdOnly : MonoBehaviour
    {
#if !HAS_AD
        private void Awake()
        {
            gameObject.SetActive(false);
        }
#endif        
    }
}