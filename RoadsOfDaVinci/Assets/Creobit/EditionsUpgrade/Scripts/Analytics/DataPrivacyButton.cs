using UnityEngine;
using UnityEngine.UI;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/DataPrivacy/DataPrivacyButton")]
    [RequireComponent(typeof(Button))]
    public class DataPrivacyButton : MonoBehaviour
    {
        [SerializeField] private DataPrivacyWindow _dataPrivacyWindow;
        private Button _button;

        private void Awake()
        {
#if !UNITY_ANALYTICS
            DestroyImmediate(this);
            return;
#endif
            
            _button ??= GetComponent<Button>();
            _button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
#if UNITY_ANALYTICS
            _button.onClick.RemoveListener(OnClick);
#endif
        }

        private void OnClick()
        {
            _dataPrivacyWindow.gameObject.SetActive(true);
        }
    }
}