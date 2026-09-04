using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [RequireComponent(typeof(Canvas))]
    public class CheatsCanvas : MonoBehaviour
    {
        private static CheatsCanvas _instance;

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            DontDestroyOnLoad(gameObject);
        }
    }
}
