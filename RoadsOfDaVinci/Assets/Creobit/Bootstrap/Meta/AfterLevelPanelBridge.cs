using _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.UI;
using Creobit.UI.Utility;
using UnityEngine;
using VContainer;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Meta
{
    public class AfterLevelPanelBridge : MonoBehaviour
    {
        [SerializeField] private int _level = 10;
        [SerializeField] private string _saveKey = "offer:afterLevel10";
        [SerializeField] private PanelReference _panel;
        [SerializeField] private Transform _parent;

        private IMapController _mapController;
        private ISaveController _saveController;
        private IUIController _uiController;

        [Inject]
        private void Construct(
            IMapController mapController,
            ISaveController saveController,
            IUIController uiController)
        {
            _mapController = mapController;
            _saveController = saveController;
            _uiController = uiController;

            _mapController.OnMapOpened += TryShow;
        }

        private void OnDestroy()
        {
            if (_mapController != null)
            {
                _mapController.OnMapOpened -= TryShow;
            }
        }

        public void TryShow()
        {
            if (_panel == null || _uiController == null || _saveController?.CurrentSaveData == null)
            {
                return;
            }

            if (_saveController.CurrentSaveData.LastPassedLevel != _level)
            {
                return;
            }

            if (_saveController.Service.IsComicsPassed(_saveKey))
            {
                return;
            }

            _uiController.ShowPanel(_panel, _parent);
            _saveController.Service.SaveComics(_saveKey);
            _saveController.Save();
        }

        public void Hide()
        {
            if (_panel == null || _uiController == null)
            {
                return;
            }

            _uiController.HidePanel(_panel, _parent);
        }
    }
}
