using Creobit.UI.Utility;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using VContainer;

namespace Creobit.UI
{
    public class UILoader : MonoBehaviour
    {
        [SerializeField] private ScenePanelReference[] _scenePanels;
        [SerializeField] private LoadablePanelReference[] _starterPanels;
        [SerializeField] private LoadablePanelReference[] _loadedPanels;
        [SerializeField] private PreservedPanelReference[] _preservedPanels;
        [SerializeField] private TMP_FontAsset _defaultFont;
        [SerializeField] private TMP_FontAsset _turkishFont;
        [SerializeField] private bool _loadPanelsInParallel;

        private IUIController _uiController;

        [Inject]
        private void Construct(IUIController uiController)
        {
            _uiController = uiController;
        }

        private void OnDestroy()
        {
            UnloadPanels();
        }

        public UniTask LoadPanels()
        {
            _uiController.CachePanels(_scenePanels);

            return UniTask.WhenAll(
                _uiController.LoadPanels(_loadedPanels, false, _loadPanelsInParallel),
                _uiController.LoadPanels(_starterPanels, true, _loadPanelsInParallel),
                _uiController.PreservePanels(_preservedPanels, _loadPanelsInParallel));
        }

        private void UnloadPanels()
        {
            foreach (var loaderPanel in _loadedPanels)
            {
                _uiController.UnloadPanel(loaderPanel.Reference);
            }

            foreach (var startPanel in _starterPanels)
            {
                _uiController.UnloadPanel(startPanel.Reference);
            }
            
            foreach (var scenePanel in _scenePanels)
            {
                _uiController.UnloadPanel(scenePanel.Reference);
            }
        }
    }
}
