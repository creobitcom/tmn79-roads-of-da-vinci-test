using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes.UI
{
    public class CutscenePanelView : MonoBehaviour
    {
        [Header("Background")]
        [SerializeField] private Image _darkBackground;

        [Header("Layout")]
        [Tooltip("Группа, центрирующая 1/2/3 фото. После раскладки замораживается, чтобы можно было двигать/масштабировать слоты.")]
        [SerializeField] private HorizontalLayoutGroup _layoutGroup;

        [Header("Frames Setup (Strictly 3 slots)")]
        [SerializeField] private List<CanvasGroup> _frameGroups;
        [SerializeField] private List<Image> _frameImages;
        [Tooltip("Кнопки на слотах для увеличения фото по клику (по одной на слот, тот же порядок).")]
        [SerializeField] private List<Button> _frameButtons;

        [Header("Pop-up")]
        [Tooltip("Начальный масштаб кадра перед pop-up анимацией (1 = без увеличения).")]
        [SerializeField] private float _popStartScale = 0.6f;

        [Header("Close button")]
        [SerializeField] private Button _closeButton;
        [Tooltip("Прогресс закрытия окна (Image type = Filled). Заполняется за суммарное время показа всех фото.")]
        [SerializeField] private Image _closeProgress;

        [Header("Zoom")]
        [Tooltip("Во сколько раз увеличивается фото при клике.")]
        [SerializeField] private float _zoomScale = 2f;
        [Tooltip("Время анимации зума (одновременное движение в центр + увеличение), сек.")]
        [SerializeField] private float _zoomDuration = 0.3f;
        [Tooltip("Сила пружины (overshoot) при зуме. 1 = без пружины, больше = сильнее отскок.")]
        [SerializeField] private float _zoomOvershoot = 1.7f;
        [Tooltip("Дополнительное смещение фото при зуме относительно его собственной позиции (0,0 = увеличивается на месте).")]
        [SerializeField] private Vector2 _zoomCenterOffset = Vector2.zero;

        public event Action CloseRequested;
        public event Action<bool> ZoomStateChanged;
        public event Action TimerStopRequested;
        public event Action SkipPhotoRequested;

        private readonly List<Vector3> _slotHomeLocalPos = new();
        private readonly List<Vector3> _slotHomeEuler = new();
        private readonly List<int> _slotHomeSibling = new();
        private readonly List<bool> _revealed = new();
        private readonly List<bool> _started = new();
        private int _zoomedIndex = -1;
        private bool _wired;

        private void WireButtons()
        {
            if (_wired) return;
            _wired = true;

            if (_closeButton != null)
                _closeButton.onClick.AddListener(() => CloseRequested?.Invoke());

            WireBackgroundClick();

            if (_frameButtons != null)
            {
                for (int i = 0; i < _frameButtons.Count; i++)
                {
                    if (_frameButtons[i] == null) continue;
                    int index = i;
                    _frameButtons[i].onClick.AddListener(() => OnSlotClicked(index));
                }
            }
        }

        private void WireBackgroundClick()
        {
            if (_darkBackground == null) return;

            _darkBackground.raycastTarget = true;

            var trigger = _darkBackground.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = _darkBackground.gameObject.AddComponent<EventTrigger>();

            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(_ =>
            {
                // Клик в пустоту при открытом фото — сброс зума, не промотка.
                if (_zoomedIndex >= 0)
                {
                    ZoomOut();
                    return;
                }

                TimerStopRequested?.Invoke();
                SkipPhotoRequested?.Invoke();
            });
            trigger.triggers.Add(entry);
        }

        public void SetupLayout(int totalFramesCount)
        {
            WireButtons();

            if (_darkBackground != null)
                _darkBackground.gameObject.SetActive(true);

            _zoomedIndex = -1;

            int count = Mathf.Clamp(totalFramesCount, 0, _frameGroups.Count);

            if (_layoutGroup != null)
                _layoutGroup.enabled = true;

            for (int i = 0; i < _frameGroups.Count; i++)
            {
                bool isActiveSlot = i < count;
                _frameGroups[i].gameObject.SetActive(isActiveSlot);

                if (isActiveSlot)
                {
                    _frameGroups[i].alpha = 0f;
                    _frameGroups[i].blocksRaycasts = false;
                    _frameGroups[i].interactable = false;
                    _frameImages[i].sprite = null;
                }
            }

            Canvas.ForceUpdateCanvases();
            if (_layoutGroup != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_layoutGroup.transform as RectTransform);
                _layoutGroup.enabled = false;
            }

            _slotHomeLocalPos.Clear();
            _slotHomeEuler.Clear();
            _slotHomeSibling.Clear();
            _revealed.Clear();
            _started.Clear();
            for (int i = 0; i < _frameGroups.Count; i++)
            {
                var rt = _frameGroups[i].transform as RectTransform;
                _slotHomeLocalPos.Add(rt != null ? rt.localPosition : Vector3.zero);
                _slotHomeEuler.Add(rt != null ? rt.localEulerAngles : Vector3.zero);
                _slotHomeSibling.Add(_frameGroups[i].transform.GetSiblingIndex());
                _revealed.Add(false);
                _started.Add(false);
            }

            if (_closeProgress != null)
            {
                _closeProgress.gameObject.SetActive(true);
                _closeProgress.fillAmount = 0f;
            }
        }

        public bool IsPhotoRevealed(int index) =>
            index >= 0 && index < _revealed.Count && _revealed[index];

        public bool IsPhotoStarted(int index) =>
            index >= 0 && index < _started.Count && _started[index];

        public void ShowFrame(int slotIndex, Sprite sprite, float fadeDuration, float popDuration)
        {
            if (slotIndex < 0 || slotIndex >= _frameGroups.Count) return;

            CanvasGroup currentGroup = _frameGroups[slotIndex];
            Image currentImage = _frameImages[slotIndex];
            var rt = currentGroup.transform as RectTransform;

            _started[slotIndex] = true;
            _revealed[slotIndex] = false;

            currentImage.sprite = sprite;
            currentGroup.blocksRaycasts = true;
            currentGroup.interactable = true;
            currentGroup.DOKill();
            if (rt != null) rt.DOKill();

            int pending = 0;

            void OnPartComplete()
            {
                pending--;
                if (pending <= 0)
                    MarkRevealed(slotIndex);
            }

            if (fadeDuration > 0f)
            {
                pending++;
                currentGroup.DOFade(1f, fadeDuration).SetUpdate(true).OnComplete(OnPartComplete);
            }
            else
            {
                currentGroup.alpha = 1f;
            }

            if (rt != null)
            {
                if (popDuration > 0f)
                {
                    pending++;
                    rt.localScale = Vector3.one * _popStartScale;
                    rt.DOScale(Vector3.one, popDuration).SetEase(Ease.OutBack).SetUpdate(true)
                        .OnComplete(OnPartComplete);
                }
                else
                {
                    rt.localScale = Vector3.one;
                }
            }

            if (pending == 0)
                MarkRevealed(slotIndex);
        }

        public void CompleteFrameShow(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _frameGroups.Count) return;

            CanvasGroup currentGroup = _frameGroups[slotIndex];
            var rt = currentGroup.transform as RectTransform;

            _started[slotIndex] = true;

            currentGroup.DOKill();
            if (rt != null) rt.DOKill();

            currentGroup.alpha = 1f;
            if (rt != null) rt.localScale = Vector3.one;
            currentGroup.blocksRaycasts = true;
            currentGroup.interactable = true;

            MarkRevealed(slotIndex);
        }

        public void SetCloseProgress(float normalized)
        {
            if (_closeProgress != null && _closeProgress.gameObject.activeSelf)
                _closeProgress.fillAmount = Mathf.Clamp01(normalized);
        }

        public void HideCloseProgress()
        {
            if (_closeProgress != null)
                _closeProgress.gameObject.SetActive(false);
        }

        private void MarkRevealed(int index)
        {
            if (index < 0 || index >= _revealed.Count) return;
            _revealed[index] = true;
        }

        private void OnSlotClicked(int index)
        {
            TimerStopRequested?.Invoke();

            if (!IsPhotoRevealed(index))
            {
                SkipPhotoRequested?.Invoke();
                return;
            }

            if (_zoomedIndex >= 0)
            {
                if (index == _zoomedIndex)
                {
                    ZoomOut();
                    return;
                }

                // Клик по другому фото — сразу переключаем зум на него.
                ZoomOut();
            }

            ZoomIn(index);
        }

        private void ZoomIn(int index)
        {
            if (index < 0 || index >= _frameGroups.Count) return;
            if (!_frameGroups[index].gameObject.activeSelf) return;
            if (!IsPhotoRevealed(index)) return;

            var rt = _frameGroups[index].transform as RectTransform;
            if (rt == null) return;

            _zoomedIndex = index;
            ZoomStateChanged?.Invoke(true);

            rt.SetAsLastSibling();
            rt.DOKill();

            var target = GetZoomedPosition(rt, index);
            rt.DOLocalMove(target, _zoomDuration).SetUpdate(true);
            rt.DOScale(Vector3.one * _zoomScale, _zoomDuration).SetEase(Ease.OutBack, _zoomOvershoot).SetUpdate(true);
            rt.DOLocalRotate(Vector3.zero, _zoomDuration).SetUpdate(true);
        }

        /// <summary>
        /// Позиция слота при зуме: своё место (+ смещение), поджатое так,
        /// чтобы увеличенное фото не вылезло за пределы родителя.
        /// </summary>
        private Vector3 GetZoomedPosition(RectTransform rt, int index)
        {
            Vector3 home = index < _slotHomeLocalPos.Count ? _slotHomeLocalPos[index] : rt.localPosition;
            var target = new Vector3(home.x + _zoomCenterOffset.x, home.y + _zoomCenterOffset.y, home.z);

            if (rt.parent is RectTransform parent)
            {
                Rect bounds = parent.rect;
                float halfW = rt.rect.width * _zoomScale * 0.5f;
                float halfH = rt.rect.height * _zoomScale * 0.5f;

                // Если фото шире/выше родителя — центрируем по этой оси.
                target.x = halfW * 2f >= bounds.width
                    ? bounds.center.x
                    : Mathf.Clamp(target.x, bounds.xMin + halfW, bounds.xMax - halfW);
                target.y = halfH * 2f >= bounds.height
                    ? bounds.center.y
                    : Mathf.Clamp(target.y, bounds.yMin + halfH, bounds.yMax - halfH);
            }

            return target;
        }

        private void ZoomOut()
        {
            int index = _zoomedIndex;
            _zoomedIndex = -1;

            if (index < 0 || index >= _frameGroups.Count)
            {
                ZoomStateChanged?.Invoke(false);
                return;
            }

            var rt = _frameGroups[index].transform as RectTransform;
            if (rt == null)
            {
                ZoomStateChanged?.Invoke(false);
                return;
            }

            rt.DOKill();
            rt.DOLocalMove(_slotHomeLocalPos[index], _zoomDuration).SetUpdate(true);
            rt.DOScale(Vector3.one, _zoomDuration).SetEase(Ease.OutBack, _zoomOvershoot).SetUpdate(true);
            rt.DOLocalRotate(_slotHomeEuler[index], _zoomDuration).SetUpdate(true)
                .OnComplete(() => RestoreSibling(index));

            ZoomStateChanged?.Invoke(false);
        }

        private void RestoreSibling(int index)
        {
            if (index < 0 || index >= _frameGroups.Count || _frameGroups[index] == null) return;

            _frameGroups[index].transform.SetSiblingIndex(_slotHomeSibling[index]);

            // Пока слот уезжал обратно, могло открыться другое фото — оно должно остаться сверху.
            if (_zoomedIndex >= 0 && _zoomedIndex < _frameGroups.Count && _frameGroups[_zoomedIndex] != null)
                _frameGroups[_zoomedIndex].transform.SetAsLastSibling();
        }

        public void Clear()
        {
            _zoomedIndex = -1;

            foreach (var group in _frameGroups)
            {
                var rt = group.transform as RectTransform;
                if (rt != null) rt.DOKill();
                group.DOKill();
                group.alpha = 0f;
                if (rt != null) rt.localScale = Vector3.one;
                group.gameObject.SetActive(false);
            }

            foreach (var img in _frameImages)
                img.sprite = null;

            if (_closeProgress != null)
            {
                _closeProgress.fillAmount = 0f;
                _closeProgress.gameObject.SetActive(true);
            }

            if (_darkBackground != null)
                _darkBackground.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            Clear();
        }
    }
}
