using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/PlatformSpecific/UpgradeOnly")]
    public class UpgradeOnly : MonoBehaviour
    {
#if !UPGRADE
        private void Awake()
        {
            gameObject.SetActive(false);
        }
#endif  
    }
}