using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/PlatformSpecific/MobileOnly")]
    public class MobileOnly : MonoBehaviour
    {
        protected virtual void Awake()
        {
#if !UNITY_IOS && !UNITY_ANDROID
            Destroy(gameObject);
#endif
        }
    }
}