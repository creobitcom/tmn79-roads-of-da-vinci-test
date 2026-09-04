using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/AnalyticsDisableOnly")]
    public class AnalyticsDisableOnly : MonoBehaviour
    {
#if UNITY_ANALYTICS
        private void Awake()
        {
            gameObject.SetActive(false);
        }
#endif
    }
}