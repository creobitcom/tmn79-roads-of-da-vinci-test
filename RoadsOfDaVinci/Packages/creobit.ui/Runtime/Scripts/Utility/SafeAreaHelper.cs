using R3;
using R3.Triggers;
using UnityEngine;

namespace Creobit.UI.Utility
{
    /// <summary>
    /// Simple helper that adjusts UI element position to stay within safe area,
    /// only moving X or Y based on anchoring
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaHelper : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Canvas _canvas;
        private Vector3 _originalPosition;
        private Rect _lastSafeArea;
        private CompositeDisposable _disposables;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();

            if (_canvas == null)
            {
                enabled = false;
                return;
            }

            _originalPosition = _rectTransform.localPosition;
            _lastSafeArea = Screen.safeArea;

            _disposables = new CompositeDisposable();

            // Subscribe to Unity events using R3
            this.OnEnableAsObservable()
                .Subscribe(_ => AdjustPosition())
                .AddTo(_disposables);

            this.UpdateAsObservable()
                .Subscribe(_ =>
                {
                    if (_lastSafeArea != Screen.safeArea)
                    {
                        _lastSafeArea = Screen.safeArea;
                        AdjustPosition();
                    }
                })
                .AddTo(_disposables);

            AdjustPosition();
        }

        private void OnDisable()
        {
            // Return to original position when disabled
            _rectTransform.localPosition = _originalPosition;
        }

        private void OnDestroy()
        {
            _disposables?.Dispose();
        }

        private void AdjustPosition()
        {
            // Start from original position
            var newPosition = _originalPosition;

            // Get element corners in screen space
            var corners = new Vector3[4];
            _rectTransform.GetWorldCorners(corners);

            // Convert corners to screen space if using camera
            if (_canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                for (int i = 0; i < 4; i++)
                {
                    corners[i] = RectTransformUtility.WorldToScreenPoint(_canvas.worldCamera, corners[i]);
                }
            }

            // Calculate element bounds in screen space
            var minX = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            var maxX = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            var minY = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
            var maxY = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);

            // Check if we should adjust X (not stretched horizontally)
            var adjustX = !Mathf.Approximately(_rectTransform.anchorMin.x, 0f) ||
                          !Mathf.Approximately(_rectTransform.anchorMax.x, 1f);

            // Check if we should adjust Y (not stretched vertically)
            var adjustY = !Mathf.Approximately(_rectTransform.anchorMin.y, 0f) ||
                          !Mathf.Approximately(_rectTransform.anchorMax.y, 1f);

            var adjustment = Vector2.zero;

            // Calculate needed adjustments
            if (adjustX)
            {
                if (minX < Screen.safeArea.xMin)
                {
                    adjustment.x = Screen.safeArea.xMin - minX;
                }
                else if (maxX > Screen.safeArea.xMax)
                {
                    adjustment.x = Screen.safeArea.xMax - maxX;
                }
            }

            if (adjustY)
            {
                if (minY < Screen.safeArea.yMin)
                {
                    adjustment.y = Screen.safeArea.yMin - minY;
                }
                else if (maxY > Screen.safeArea.yMax)
                {
                    adjustment.y = Screen.safeArea.yMax - maxY;
                }
            }

            // If no adjustment needed, keep original position
            if (adjustment == Vector2.zero)
            {
                _rectTransform.localPosition = _originalPosition;
                return;
            }

            // Convert screen space adjustment to local space
            if (_canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                // For non-overlay canvas, convert screen offset to local offset
                var screenPos = new Vector2(minX, minY);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas.GetComponent<RectTransform>(),
                    screenPos,
                    _canvas.worldCamera,
                    out var canvasPos);

                var adjustedScreenPos = screenPos + adjustment;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas.GetComponent<RectTransform>(),
                    adjustedScreenPos,
                    _canvas.worldCamera,
                    out var adjustedCanvasPos);

                adjustment = adjustedCanvasPos - canvasPos;
            }

            // Apply adjustment
            newPosition.x += adjustment.x;
            newPosition.y += adjustment.y;
            _rectTransform.localPosition = newPosition;
        }
    }
}