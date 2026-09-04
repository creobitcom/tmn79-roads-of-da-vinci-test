using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/PremiumOnly")]
    public class PremiumOnly : MonoBehaviour
    {
#if !PREMIUM
        private void Awake()
        {
            gameObject.SetActive(false);
        }
#endif        
    }
}