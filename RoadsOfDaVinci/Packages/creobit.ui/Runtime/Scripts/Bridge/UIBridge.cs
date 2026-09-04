using Creobit.UI.Utility;
using UnityEngine;
using VContainer;

namespace Creobit.UI
{
    public class UIBridge : MonoBehaviour
    {
        [SerializeField] private Transform _panelParent;

        private IUIController _uiController;

        [Inject]
        private void Construct(IUIController uiController)
        {
            _uiController = uiController;
        }

        public void ShowPanel(PanelReference panelReference)
        {
            _uiController.ShowPanel(panelReference, _panelParent);
        }

        public void HidePanel(PanelReference panelReference)
        {
            _uiController.HidePanel(panelReference, _panelParent);
        }
    }
}