using System;
using System.Collections.Generic;
using Creobit.Localization;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Extras
{
    public class GuidePagesPresenter
    {
        private readonly GuideContentLoader _loader;
        private readonly List<GuidePageNavButton> _navButtons = new();

        private GuidesPanelView _view;
        private GuideLevelContent _content;
        private int _levelNumber;
        private int _pageIndex;

        public event Action<int> LevelOpened;
        public event Action CloseRequested;

        public GuidePagesPresenter(GuideContentLoader loader)
        {
            _loader = loader;
        }

        public bool HasContent => _content != null && _content.HasPages;

        public async UniTask<bool> Show(GuidesPanelView view, int levelNumber)
        {
            _view = view;
            _levelNumber = levelNumber;
            _pageIndex = 0;
            _content = await _loader.LoadLevel(levelNumber);

            if (_content == null || !_content.HasPages)
            {
                Log.Meta.Warning($"Guide has no pages for level {levelNumber}");
                return false;
            }

            if (_view != null)
            {
                BindButton(_view.closeButton, () => CloseRequested?.Invoke());
                BindButton(_view.nextButton, ShowNextPage);
                BindButton(_view.prevButton, ShowPrevPage);
                BuildNavButtons();
            }

            LevelOpened?.Invoke(levelNumber);
            NotifyVideoTarget(levelNumber);

            await UpdatePage();
            return true;
        }

        public void ShowPage(int pageIndex)
        {
            if (!HasContent || pageIndex < 0 || pageIndex >= _content.GuideItems.Count)
            {
                return;
            }

            _pageIndex = pageIndex;
            UpdatePage().Forget();
        }

        public void ShowNextPage() => ShowPage(_pageIndex + 1);

        public void ShowPrevPage() => ShowPage(_pageIndex - 1);

        public void Refresh()
        {
            if (HasContent)
            {
                UpdatePage().Forget();
            }
        }

        public void Clear()
        {
            ClearNavButtons();
            _content = null;
            _pageIndex = 0;
            _view = null;
        }

        private async UniTask UpdatePage()
        {
            if (!HasContent)
            {
                return;
            }

            _pageIndex = Mathf.Clamp(_pageIndex, 0, _content.GuideItems.Count - 1);

            var page = _content.GuideItems[_pageIndex];

            if (_view != null)
            {
                if (_view.pageText != null)
                {
                    _view.pageText.text = string.IsNullOrEmpty(page.Text)
                        ? string.Empty
                        : LocalizationService.Instance.GetText(page.Text);
                }

                if (_view.prevButton != null)
                {
                    _view.prevButton.gameObject.SetActive(_pageIndex > 0);
                }

                if (_view.nextButton != null)
                {
                    _view.nextButton.gameObject.SetActive(_pageIndex + 1 < _content.GuideItems.Count);
                }
            }

            UpdateNavSelection();

            var sprite = await _loader.LoadPicture(_levelNumber, page.PictureName);

            if (sprite == null || _view == null)
            {
                return;
            }

            if (_view.pageRawImage != null)
            {
                _view.pageRawImage.texture = sprite.texture;
            }

            if (_view.pageImage != null)
            {
                _view.pageImage.sprite = sprite;
            }
        }

        private void NotifyVideoTarget(int levelNumber)
        {
            if (_view == null)
            {
                return;
            }

            var target = _view.GetComponentInChildren<IGuideVideoTarget>(true);
            target?.ShowLevelVideo(levelNumber);
        }

        private void BuildNavButtons()
        {
            ClearNavButtons();

            if (_view.navButtonTemplate == null || _view.navButtonsRoot == null)
            {
                return;
            }

            _view.navButtonTemplate.gameObject.SetActive(false);

            for (var i = 0; i < _content.GuideItems.Count; i++)
            {
                var button = Object.Instantiate(_view.navButtonTemplate, _view.navButtonsRoot);
                button.gameObject.SetActive(true);
                button.Initialize(i);
                button.Clicked += ShowPage;
                _navButtons.Add(button);
            }
        }

        private void UpdateNavSelection()
        {
            foreach (var button in _navButtons)
            {
                if (button != null)
                {
                    button.SetSelected(button.PageIndex == _pageIndex);
                }
            }
        }

        private void ClearNavButtons()
        {
            foreach (var button in _navButtons)
            {
                if (button == null)
                {
                    continue;
                }

                button.Clicked -= ShowPage;
                Object.Destroy(button.gameObject);
            }

            _navButtons.Clear();
        }

        private static void BindButton(Button button, Action callback)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => callback());
        }
    }
}
