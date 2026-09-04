using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/AnalyticsOnly")]
    public class AnalyticsOnly : MonoBehaviour
    {
#if !UNITY_ANALYTICS
        private void Awake()
        {
            gameObject.SetActive(false);
        }
#endif
    }
}