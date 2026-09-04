using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/EditionSpecific/PlatformSpecific/UpgradePositionOffset")]
    [RequireComponent(typeof(RectTransform))]
    public class UpgradePositionOffset : MonoBehaviour
    {
        [SerializeField] private Vector2 _upgradeOffset = new Vector2(0f, 53f);

        private void Awake()
        {
#if UPGRADE
            var rectTransform = (RectTransform)transform;
            rectTransform.anchoredPosition += _upgradeOffset;
#endif
        }
    }
}
