using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/PlatformSpecific/NotUpgrade")]
    public class NotUpgrade : MonoBehaviour
    {
#if UPGRADE
        private void Awake()
        {
            gameObject.SetActive(false);
        }
#endif  
    }
}