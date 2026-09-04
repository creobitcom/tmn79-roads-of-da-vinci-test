using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    public class TesterOnly : MonoBehaviour
    {
        private TestersFeatures _testersFeatures;

        private void Awake()
        {
            _testersFeatures = TestersFeatures.Instance;

            if (_testersFeatures == null)
            {
                Destroy(gameObject);
                return;
            }

            if (_testersFeatures.IsReady)
            {
                ApplyAvailability();
                return;
            }

            gameObject.SetActive(false);

            _testersFeatures.Ready += ApplyAvailability;
        }

        private void OnDestroy()
        {
            if (_testersFeatures != null)
            {
                _testersFeatures.Ready -= ApplyAvailability;
            }
        }

        private void ApplyAvailability()
        {
            if (_testersFeatures != null)
            {
                _testersFeatures.Ready -= ApplyAvailability;
            }

            if (_testersFeatures != null && _testersFeatures.IsAvailable)
            {
                gameObject.SetActive(true);
                return;
            }

            Destroy(gameObject);
        }
    }
}
