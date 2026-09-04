using Unity.VideoHelper;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.EditionsUpgrade
{
    [RequireComponent(typeof(VideoGuidesController))]
    public class VideoGuidesPresenter : MonoBehaviour
    {
        [Header("Options")]
        [SerializeField, Tooltip("Show/Hide buttons will be use inner selected sprites and outer tabs sprites")]
        private bool _useTabs;
        [SerializeField] private bool _validateButtons = true;
        [SerializeField] private bool _keepTabsAlwaysVisible;
        [SerializeField] private bool _selectVideoTabOnOpen;

        [Header("View Buttons Sprites")]
        [SerializeField] private Sprite _selectedHideSprite;
        [SerializeField] private Sprite _deselectedHideSprite;
        [SerializeField] private Sprite _selectedShowSprite;
        [SerializeField] private Sprite _deselectedShowSprite;
        [SerializeField] private Sprite _selectedTabSprite;
        [SerializeField] private Sprite _deselectedTabSprite;

        [Header("UI")]
        [SerializeField] private Canvas _youtubeCanvas;
        [SerializeField] private Button _showButton;
        [SerializeField] private Button _hideButton;
        [SerializeField] private GameObject _loadingPanel;
        [SerializeField] private GameObject _noConnectionPanel;
        [SerializeField] private GameObject _pagesScreen;
        [SerializeField] private GameObject _pagesInfo;
        [SerializeField] private GameObject _videoRoot;
        [SerializeField] private Transform _position;
        [SerializeField] private CanvasGroup _panelCanvasGroup;
        [SerializeField] private RawImage _pageRawImage;

        [Header("Components")]
        [SerializeField] private VideoGuidesController _controller;
        [SerializeField] private VideoPresenter _videoPresenter;

        [Header("Folder Frame")]
        [SerializeField] private Image _folderFrameImage;
        [SerializeField] private Sprite _screenshotsFrameSprite;
        [SerializeField] private Sprite _videoFrameSprite;

        [Header("Youtube")]
        [SerializeField] private Button _youtubeIconButton;

        private Image _selectShowImage;
        private Image _selectHideImage;
        private bool _isVideoTabActive;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_validateButtons == false) return;

            var showBtnImg = _showButton != null ? _showButton.GetComponent<Image>() : null;
            var hideBtnImg = _hideButton != null ? _hideButton.GetComponent<Image>() : null;

            if (showBtnImg != null && _selectedShowSprite != null)
            {
                showBtnImg.sprite = _selectedShowSprite;
            }

            if (hideBtnImg != null && _selectedHideSprite != null)
            {
                hideBtnImg.sprite = _selectedHideSprite;
            }

            SetFirstChildActive(_showButton, true);
            SetFirstChildActive(_hideButton, true);
        }
#endif

        private void OnDestroy()
        {
            _showButton.onClick.RemoveListener(OnShowButtonClicked);
            _hideButton.onClick.RemoveListener(OnHideButtonClicked);

            _youtubeIconButton.onClick.RemoveListener(_controller.OpenYoutubeVideo);

            _controller.OnError -= VideoStoppedHandler;
            _controller.OnFail -= VideoStoppedHandler;
            _controller.OnSuccess -= VideoStartedHandler;
            _controller.OnLoadStateChanged -= UpdateVideoStatePanels;
        }

        private void Awake()
        {
            _showButton.onClick.AddListener(OnShowButtonClicked);
            _hideButton.onClick.AddListener(OnHideButtonClicked);

            _youtubeIconButton.onClick.AddListener(_controller.OpenYoutubeVideo);

            _selectShowImage = _showButton.GetComponent<Image>();
            _selectHideImage = _hideButton.GetComponent<Image>();

            SetFirstChildActive(_showButton, true);
            SetFirstChildActive(_hideButton, true);

            _controller.OnError += VideoStoppedHandler;
            _controller.OnFail += VideoStoppedHandler;
            _controller.OnSuccess += VideoStartedHandler;
            _controller.OnLoadStateChanged += UpdateVideoStatePanels;

            if (_panelCanvasGroup != null)
            {
                _panelCanvasGroup.alpha = 0f;
            }

            _isVideoTabActive = _selectVideoTabOnOpen;
            SetButtonsViewState(_isVideoTabActive);
            ApplyTabVisibility(_isVideoTabActive);
        }

        private void OnEnable()
        {
            if (_panelCanvasGroup != null)
            {
                _panelCanvasGroup.alpha = 0f;
            }

            _isVideoTabActive = _selectVideoTabOnOpen;
            SetButtonsViewState(_isVideoTabActive);
            ApplyTabVisibility(_isVideoTabActive);

            _hideButton.gameObject.SetActive(_keepTabsAlwaysVisible || _controller.IsExistsVideo);
            _showButton.gameObject.SetActive(_keepTabsAlwaysVisible || _controller.IsExistsVideo);
            _youtubeIconButton.gameObject.SetActive(_useTabs == false && _controller.IsNeedShowYoutube());
        }

        private void Update()
        {
            if (_panelCanvasGroup != null && _panelCanvasGroup.alpha < 1f)
            {
                if (_isVideoTabActive || _pageRawImage == null || _pageRawImage.texture != null)
                {
                    _panelCanvasGroup.alpha = 1f;
                }
            }
        }

        private void ApplyTabVisibility(bool isVideo)
        {
            if (_pagesScreen != null) _pagesScreen.SetActive(!isVideo);
            if (_pagesInfo != null) _pagesInfo.SetActive(!isVideo);
            if (_videoRoot != null) _videoRoot.SetActive(isVideo);

            if (_folderFrameImage != null)
            {
                if (isVideo && _videoFrameSprite != null)
                {
                    _folderFrameImage.sprite = _videoFrameSprite;
                }
                else if (!isVideo && _screenshotsFrameSprite != null)
                {
                    _folderFrameImage.sprite = _screenshotsFrameSprite;
                }
            }

            _youtubeCanvas.transform.position = _position.position;
            _youtubeCanvas.gameObject.SetActive(isVideo);
            _videoPresenter.gameObject.SetActive(isVideo);

            if (isVideo)
            {
                UpdateVideoStatePanels();

                if (_controller.IsExistsVideo)
                {
                    _controller.Play();
                }
            }
            else
            {
                UpdateVideoStatePanels();

                _controller.Pause();
            }
        }

        private void UpdateVideoStatePanels()
        {
            if (_isVideoTabActive == false)
            {
                _loadingPanel?.SetActive(false);
                _noConnectionPanel?.SetActive(false);

                return;
            }

            var isLoading = _controller.IsLoadingVideo;

            _loadingPanel?.SetActive(isLoading);
            _noConnectionPanel?.SetActive(isLoading == false && _controller.IsExistsVideo == false);
        }

        private void OnShowButtonClicked()
        {
            if (_isVideoTabActive) return;

            _isVideoTabActive = true;
            SetButtonsViewState(true);
            ApplyTabVisibility(true);
        }

        private void OnHideButtonClicked()
        {
            if (!_isVideoTabActive) return;

            _isVideoTabActive = false;
            SetButtonsViewState(false);
            ApplyTabVisibility(false);
        }

        private static Transform FirstChildOrNull(Button button)
        {
            if (button == null || button.transform.childCount == 0)
            {
                return null;
            }

            return button.transform.GetChild(0);
        }

        private static void SetFirstChildActive(Button button, bool state)
        {
            var child = FirstChildOrNull(button);

            if (child != null)
            {
                child.gameObject.SetActive(state);
            }
        }

        private void Start()
        {
        }

        private void OnDisable()
        {
            _videoPresenter.SetPlayPauseSprite();
        }

        private void VideoStartedHandler()
        {
            _videoPresenter.SetPlayPauseSprite();

            if (_isVideoTabActive)
            {
                _youtubeCanvas.transform.position = _position.position;
                _videoPresenter.gameObject.SetActive(true);
                _youtubeCanvas.gameObject.SetActive(true);
            }

            UpdateVideoStatePanels();

            _hideButton.gameObject.SetActive(_keepTabsAlwaysVisible || _controller.IsExistsVideo);
            _showButton.gameObject.SetActive(_keepTabsAlwaysVisible || _controller.IsExistsVideo);

            _youtubeIconButton.gameObject.SetActive(_useTabs == false && _controller.IsNeedShowYoutube());
        }

        private void VideoStoppedHandler()
        {
            _videoPresenter.SetPlayPauseSprite();

            if (_isVideoTabActive)
            {
                _youtubeCanvas.transform.position = _position.position;
                _youtubeCanvas.gameObject.SetActive(true);
                _videoPresenter.gameObject.SetActive(true);
            }
            else
            {
                _youtubeCanvas.gameObject.SetActive(false);
                _videoPresenter.gameObject.SetActive(false);
            }

            UpdateVideoStatePanels();

            _youtubeIconButton.gameObject.SetActive(_useTabs == false && _controller.IsNeedShowYoutube());

            _hideButton.gameObject.SetActive(_keepTabsAlwaysVisible || _controller.IsExistsVideo);
            _showButton.gameObject.SetActive(_keepTabsAlwaysVisible || _controller.IsExistsVideo);
        }

        private void SetButtonsViewState(bool isShowed)
        {
            if (isShowed)
            {
                _selectHideImage.sprite = _deselectedHideSprite;
                _selectShowImage.sprite = _selectedShowSprite;

                UpdateTabWidth(_hideButton, _deselectedHideSprite);
                UpdateTabWidth(_showButton, _selectedShowSprite);
            }
            else
            {
                _selectHideImage.sprite = _selectedHideSprite;
                _selectShowImage.sprite = _deselectedShowSprite;

                UpdateTabWidth(_hideButton, _selectedHideSprite);
                UpdateTabWidth(_showButton, _deselectedShowSprite);
            }
        }

        private static void UpdateTabWidth(Button button, Sprite sprite)
        {
            if (button == null || sprite == null) return;
            var rt = button.transform as RectTransform;
            if (rt == null) return;
            var size = rt.sizeDelta;
            size.x = sprite.rect.width;
            rt.sizeDelta = size;
        }
    }
}