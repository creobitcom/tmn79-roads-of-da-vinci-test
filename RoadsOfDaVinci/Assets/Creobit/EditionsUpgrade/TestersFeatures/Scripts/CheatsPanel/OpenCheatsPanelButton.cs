using UnityEngine;
using UnityEngine.UI;

namespace Creobit.EditionsUpgrade
{
    [RequireComponent(typeof(Button))]
    public class OpenCheatsPanelButton : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [SerializeField]
        private CheatsPanel _cheatsPanel;

        private void OnValidate()
        {
            _button ??= GetComponent<Button>();
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }

        private void Awake()
        {
            _button.onClick.AddListener(OnButtonClicked);
        }

        private void OnButtonClicked()
        {
            _cheatsPanel.SetVisible(true);
        }
    }
}
