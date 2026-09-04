using UnityEngine;
using UnityEngine.UI;

namespace EditionsUpgrade.Scripts.LinkButtons
{
    [RequireComponent(typeof(Button))]
    public class LinkButton : MonoBehaviour
    {
        [SerializeField] private string _link;
    
        [SerializeField] private Button _button;

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(OpenLink);
        }

        private void Awake()
        {
            _button ??= GetComponent<Button>();
        
            _button.onClick.AddListener(OpenLink);
        }

        private void OpenLink()
        {
            Application.OpenURL(_link);
        }
    }
}
