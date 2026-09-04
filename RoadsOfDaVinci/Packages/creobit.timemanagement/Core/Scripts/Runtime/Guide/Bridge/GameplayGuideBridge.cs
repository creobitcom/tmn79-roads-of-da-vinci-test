using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.Extras;
using _8floor.TimeManagement.Core.Scripts.Runtime.Guide.View;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using Creobit.AddressablesController;
using Creobit.Logger;
using Creobit.UI;
using Creobit.UI.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Guide.Bridge
{
    public class GameplayGuideBridge : MonoBehaviour
    {
        [SerializeField] private PanelReference _guidePanel;
        [SerializeField] private AllLevelsSO _allLevels;
        [SerializeField] private PanelReference _pagesPanel;
        [SerializeField] private string _pagesContentFolder;
        [SerializeField] private Transform _pagesPanelParent;

        private AssetReferenceT<Sprite> _guideReference;
        private GuidePagesPresenter _pagesPresenter;

        private IUIController _uiController;
        private ILevelLoader _levelLoader;
        private IAddressablesController _addressablesController;
        private IPauseController _pauseController;
        private bool _isGuideOpen;

        private bool UsePages => _pagesPanel != null && !string.IsNullOrWhiteSpace(_pagesContentFolder);

        [Inject]
        private void Construct(IUIController uiController,
            ILevelLoader levelLoader, IAddressablesController addressablesController,
            IPauseController pauseController = null)
        {
            _uiController = uiController;
            _levelLoader = levelLoader;
            _addressablesController = addressablesController;
            _pauseController = pauseController;
        }

        private void OnDestroy()
        {
            if (_pagesPresenter != null)
            {
                _pagesPresenter.CloseRequested -= HideGuidePanel;
                _pagesPresenter.Clear();
            }

            if (_guideReference == null)
            {
                return;
            }

            _addressablesController.UnloadAssetReference(_guideReference);
        }

        public void ShowGuidePanel()
        {
            if (_levelLoader.LevelBaseSO.CurrentValue == null)
            {
                Log.Gameplay.Error("Guide data is missing or not loaded yet.");
                return;
            }

            _isGuideOpen = true;
            _pauseController?.Pause();

            var levelNumber = _levelLoader.LevelBaseSO.CurrentValue.LevelNumber;

            if (UsePages)
            {
                ShowPagesPanel(levelNumber).Forget();
                return;
            }

            ShowSpritePanel(levelNumber).Forget();
        }

        public void HideGuidePanel()
        {
            if (_isGuideOpen)
            {
                _isGuideOpen = false;
                _pauseController?.Resume();
            }

            if (UsePages)
            {
                _uiController.HidePanel(_pagesPanel, PagesParent());
                _pagesPresenter?.Clear();
                return;
            }

            _uiController.HidePanel(_guidePanel, null);
        }

        private Transform PagesParent()
        {
            if (_pagesPanelParent != null)
            {
                return _pagesPanelParent;
            }

            var canvas = GetComponentInParent<Canvas>();

            if (canvas == null)
            {
                canvas = FindFirstObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                return null;
            }

            var layer = canvas.transform.Find("Layer3") ?? canvas.transform.Find("Layer2") ?? canvas.rootCanvas.transform;

            return layer;
        }

        private async UniTaskVoid ShowPagesPanel(int levelNumber)
        {
            if (_pagesPresenter == null)
            {
                _pagesPresenter = new GuidePagesPresenter(new GuideContentLoader(_pagesContentFolder));
                _pagesPresenter.CloseRequested += HideGuidePanel;
            }

            _uiController.ShowPanel(_pagesPanel, PagesParent());

            var panel = await _uiController.GetPanel(_pagesPanel);
            var view = panel == null ? null : panel.GetComponent<GuidesPanelView>();

            if (view == null)
            {
                Log.Gameplay.Error("Guide pages panel has no GuidesPanelView.");
                if (_isGuideOpen)
                {
                    _isGuideOpen = false;
                    _pauseController?.Resume();
                }
                return;
            }

            if (!await _pagesPresenter.Show(view, levelNumber))
            {
                if (_isGuideOpen)
                {
                    _isGuideOpen = false;
                    _pauseController?.Resume();
                }
                _uiController.HidePanel(_pagesPanel, PagesParent());
            }
        }

        private async UniTaskVoid ShowSpritePanel(int levelNumber)
        {
            var guidePanel = await _uiController.ShowPanel<GuidePanelView>(_guidePanel, null);

            _guideReference = _allLevels.AllLevels
                .First(level => level.levelNum == levelNumber).guide;

            var guide = await _addressablesController.LoadAssetByReferenceAsync<Sprite>(_guideReference);

            guidePanel.SetGuide(guide);
        }
    }
}
