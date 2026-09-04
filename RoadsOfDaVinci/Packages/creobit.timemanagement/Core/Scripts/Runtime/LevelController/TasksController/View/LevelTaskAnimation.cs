using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.View
{
    // TODO : fix it later and move inputs to GameplayInputSystem
    public class LevelTaskAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Movement Settings")] 
        [SerializeField] private float moveDistance = 320f;
        [SerializeField] private float moveDuration = 0.3f;
        [SerializeField] private Ease easeType = Ease.OutQuad;

        private RectTransform _rectTransform;
        private Vector3 _originalPosition;
        private Vector3 _targetPosition;
        private bool _isMovedLeft;
        private Tween _currentTween;

        // For tracking position changes
        private Vector3 _lastKnownPosition;
        private bool _isInitialized;

        private void Start()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null)
            {
                Debug.LogError("UIHoverMover requires a RectTransform component!");
                enabled = false;
                return;
            }

            InitializePosition();
        }

        private void LateUpdate()
        {
            // Check if the position changed due to layout group changes
            if (_isInitialized && !_isMovedLeft && _rectTransform.anchoredPosition != (Vector2)_lastKnownPosition)
            {
                UpdateOriginalPosition();
            }
        }

        private void InitializePosition()
        {
            _originalPosition = _rectTransform.anchoredPosition;
            _lastKnownPosition = _originalPosition;
            _targetPosition = _originalPosition + Vector3.left * moveDistance;
            _isInitialized = true;
        }

        private void UpdateOriginalPosition()
        {
            _originalPosition = _rectTransform.anchoredPosition;
            _lastKnownPosition = _originalPosition;
            _targetPosition = _originalPosition + Vector3.left * moveDistance;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_isMovedLeft)
            {
                MoveToLeft();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_isMovedLeft)
            {
                MoveToOriginal();
            }
        }

        private void MoveToLeft()
        {
            _isMovedLeft = true;

            // Kill any existing tween to prevent conflicts
            _currentTween?.Kill();

            _currentTween = _rectTransform.DOAnchorPos(_targetPosition, moveDuration)
                .SetEase(easeType);
        }

        private void MoveToOriginal()
        {
            // Kill any existing tween to prevent conflicts
            _currentTween?.Kill();

            _currentTween = _rectTransform.DOAnchorPos(_originalPosition, moveDuration)
                .SetEase(easeType)
                .OnComplete(() =>
                {
                    _isMovedLeft = false;
                    // Update last known position after returning to original
                    _lastKnownPosition = _originalPosition;
                });
        }

        // Optional: Reset position when the object is disabled
        private void OnDisable()
        {
            if (_rectTransform != null)
            {
                _currentTween?.Kill();
                _rectTransform.anchoredPosition = _originalPosition;
                _isMovedLeft = false;
                _lastKnownPosition = _originalPosition;
            }
        }

        // Clean up on destroy
        private void OnDestroy()
        {
            _currentTween?.Kill();
        }
    }
}