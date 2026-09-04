using System;
using DG.Tweening;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.UI
{
    public enum PanelTransitionType
    {
        None,
        SlideFromBottom,
        SlideFromTop,
        SlideFromLeft,
        SlideFromRight,
        Fade,
        Scale
    }

    [RequireComponent(typeof(RectTransform))]
    public class PanelTransitionAnimator : MonoBehaviour
    {
        [Header("Появление")]
        [SerializeField] private PanelTransitionType showTransition = PanelTransitionType.SlideFromBottom;
        [SerializeField] private float showDuration = 0.35f;
        [SerializeField] private Ease showEase = Ease.OutCubic;

        [Header("Исчезновение")]
        [SerializeField] private PanelTransitionType hideTransition = PanelTransitionType.SlideFromBottom;
        [SerializeField] private float hideDuration = 0.25f;
        [SerializeField] private Ease hideEase = Ease.InCubic;

        [Header("Общее")]
        [Tooltip("Запас, на который панель уезжает за край экрана.")]
        [SerializeField] private float offscreenPadding = 40f;

        [Tooltip("Нужен только для Fade. Пусто — будет добавлен автоматически.")]
        [SerializeField] private CanvasGroup canvasGroup;

        private RectTransform _rectTransform;
        private Vector2 _shownPosition;
        private Vector3 _shownScale;
        private bool _initialized;
        private Tween _currentTween;

        private void Awake() => Initialize();

        private void OnDestroy() => KillCurrentTween();

        public void PlayShow()
        {
            KillCurrentTween();
            ResetToShownState();

            if (showTransition == PanelTransitionType.None || showDuration <= 0f)
            {
                return;
            }

            switch (showTransition)
            {
                case PanelTransitionType.Fade:
                    var group = GetOrCreateCanvasGroup();
                    group.alpha = 0f;
                    _currentTween = group.DOFade(1f, showDuration);
                    break;

                case PanelTransitionType.Scale:
                    _rectTransform.localScale = Vector3.zero;
                    _currentTween = _rectTransform.DOScale(_shownScale, showDuration);
                    break;

                default:
                    _rectTransform.anchoredPosition = GetOffscreenPosition(showTransition);
                    _currentTween = _rectTransform.DOAnchorPos(_shownPosition, showDuration);
                    break;
            }

            _currentTween.SetEase(showEase == Ease.Unset ? Ease.OutCubic : showEase).SetUpdate(true);
        }

        public void PlayHide(Action onComplete)
        {
            KillCurrentTween();

            if (hideTransition == PanelTransitionType.None || hideDuration <= 0f || !gameObject.activeInHierarchy)
            {
                ResetToShownState();
                onComplete?.Invoke();
                return;
            }

            switch (hideTransition)
            {
                case PanelTransitionType.Fade:
                    _currentTween = GetOrCreateCanvasGroup().DOFade(0f, hideDuration);
                    break;

                case PanelTransitionType.Scale:
                    _currentTween = _rectTransform.DOScale(Vector3.zero, hideDuration);
                    break;

                default:
                    _currentTween = _rectTransform.DOAnchorPos(GetOffscreenPosition(hideTransition), hideDuration);
                    break;
            }

            _currentTween.SetEase(hideEase == Ease.Unset ? Ease.InCubic : hideEase).SetUpdate(true).OnComplete(() =>
            {
                onComplete?.Invoke();
                ResetToShownState();
            });
        }

        public void HideImmediately(Action onComplete)
        {
            KillCurrentTween();
            ResetToShownState();

            onComplete?.Invoke();
        }

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _rectTransform = (RectTransform)transform;
            _shownPosition = _rectTransform.anchoredPosition;
            _shownScale = _rectTransform.localScale;
            _initialized = true;
        }

        private void ResetToShownState()
        {
            Initialize();

            _rectTransform.anchoredPosition = _shownPosition;
            _rectTransform.localScale = _shownScale;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        private void KillCurrentTween()
        {
            _currentTween?.Kill();
            _currentTween = null;
        }

        private CanvasGroup GetOrCreateCanvasGroup()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            return canvasGroup;
        }

        private Vector2 GetOffscreenPosition(PanelTransitionType transition)
        {
            Initialize();

            var parent = _rectTransform.parent as RectTransform;
            var parentSize = parent != null ? parent.rect.size : new Vector2(Screen.width, Screen.height);
            var size = _rectTransform.rect.size;
            var pivot = _rectTransform.pivot;
            var anchorMin = _rectTransform.anchorMin;
            var anchorMax = _rectTransform.anchorMax;

            var position = _shownPosition;

            switch (transition)
            {
                case PanelTransitionType.SlideFromBottom:
                    position.y = -(size.y * (1f - pivot.y) + offscreenPadding) - anchorMin.y * parentSize.y;
                    break;

                case PanelTransitionType.SlideFromTop:
                    position.y = parentSize.y + size.y * pivot.y + offscreenPadding - anchorMax.y * parentSize.y;
                    break;

                case PanelTransitionType.SlideFromLeft:
                    position.x = -(size.x * (1f - pivot.x) + offscreenPadding) - anchorMin.x * parentSize.x;
                    break;

                case PanelTransitionType.SlideFromRight:
                    position.x = parentSize.x + size.x * pivot.x + offscreenPadding - anchorMax.x * parentSize.x;
                    break;
            }

            return position;
        }
    }
}
