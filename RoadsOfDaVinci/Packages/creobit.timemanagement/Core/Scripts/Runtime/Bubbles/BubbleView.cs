using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Bubbles
{
    [ExecuteAlways]
    public class BubbleView : MonoBehaviour
    {
        [Header("Main References")] 
        [SerializeField] private RectTransform _mainRect;
        [SerializeField] private TMP_Text _textSpeak;
        [SerializeField] private Button _buttonSkip;

        [Header("Procedural Visuals References")]
        [SerializeField] private RectTransform _contentRect;
        [SerializeField] private RectTransform _bodyOutline;
        [SerializeField] private ProceduralTail _tailOutline;
        [SerializeField] private RectTransform _bodyFill;
        [SerializeField] private ProceduralTail _tailFill;

        [Header("Settings")] 
        [SerializeField] private float _borderThickness = 4f;
        [SerializeField] private float _tailBaseWidth = 30f;
        [SerializeField, Range(-1f, 1f)] private float _tailPositionX = 0f;
        [SerializeField] private float _maxTailLength = 50f;
        
        [Header("Debug")] 
        [SerializeField] private Vector2 _targetLocalPos = new(0, -50f);
        [SerializeField] private bool _debugShowTail = true;

        private Vector2 _lastContentSize;
        private Vector2 _lastTargetLocalPos;
        private float _lastTailPositionX;
        private float _lastTailBaseWidth;
        private float _lastMaxTailLength;
        private float _lastBorderThickness;
        private bool _lastHasAnchor;
        private bool _lastDebugShowTail;

        public RectTransform MainRect => _mainRect;
        public Button ButtonSkip => _buttonSkip;
        public bool HasAnchor { get; private set; }

        public float TailPositionX
        {
            get => _tailPositionX;
            set => _tailPositionX = value;
        }

        public void Setup(string text, bool showSkip, bool hasAnchor)
        {
            if (_textSpeak) _textSpeak.text = text;
            if (_buttonSkip) _buttonSkip.gameObject.SetActive(showSkip);
            HasAnchor = hasAnchor;

            if (_contentRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
            }
        }

        public void SetPosition(Vector2 canvasPosition)
        {
            if (_mainRect) _mainRect.localPosition = canvasPosition;
        }

        public void SetTailTarget(Vector2 localTargetPosition)
        {
            _targetLocalPos = localTargetPosition;
        }

        private void LateUpdate()
        {
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (_contentRect == null || _bodyOutline == null || _bodyFill == null) return;

            var contentSize = new Vector2(_contentRect.rect.width, _contentRect.rect.height);

            if (_lastContentSize == contentSize &&
                _lastTargetLocalPos == _targetLocalPos &&
                Mathf.Approximately(_lastTailPositionX, _tailPositionX) &&
                Mathf.Approximately(_lastTailBaseWidth, _tailBaseWidth) &&
                Mathf.Approximately(_lastMaxTailLength, _maxTailLength) &&
                Mathf.Approximately(_lastBorderThickness, _borderThickness) &&
                _lastHasAnchor == HasAnchor &&
                _lastDebugShowTail == _debugShowTail)
            {
                return;
            }

            _lastContentSize = contentSize;
            _lastTargetLocalPos = _targetLocalPos;
            _lastTailPositionX = _tailPositionX;
            _lastTailBaseWidth = _tailBaseWidth;
            _lastMaxTailLength = _maxTailLength;
            _lastBorderThickness = _borderThickness;
            _lastHasAnchor = HasAnchor;
            _lastDebugShowTail = _debugShowTail;

            _bodyFill.sizeDelta = contentSize;
            _bodyOutline.sizeDelta = new Vector2(contentSize.x + _borderThickness * 2, contentSize.y + _borderThickness * 2);

            var isTailOffsetZero = _targetLocalPos.sqrMagnitude < 0.001f;
            var shouldShowTail = (HasAnchor || _debugShowTail) && !isTailOffsetZero;

            if (_tailOutline) _tailOutline.gameObject.SetActive(shouldShowTail);
            if (_tailFill) _tailFill.gameObject.SetActive(shouldShowTail);

            if (!shouldShowTail || _tailOutline == null || _tailFill == null) return;

            var maxOffset = (contentSize.x / 2f) - (_tailBaseWidth / 2f) - _borderThickness;
            var rootOffsetX = maxOffset * _tailPositionX;
            var tailRootPos = new Vector2(rootOffsetX, 0);

            var finalTargetPos = _targetLocalPos;
            var dirToTarget = finalTargetPos - tailRootPos;

            if (_maxTailLength > 0 && dirToTarget.magnitude > _maxTailLength)
            {
                finalTargetPos = tailRootPos + dirToTarget.normalized * _maxTailLength;
            }

            _tailOutline.BaseWidth = _tailBaseWidth + _borderThickness * 2;
            _tailOutline.BaseOffset = tailRootPos;
            _tailOutline.TargetPoint = finalTargetPos;

            _tailFill.BaseWidth = _tailBaseWidth;
            _tailFill.BaseOffset = new Vector2(rootOffsetX, _borderThickness);

            var fillDir = finalTargetPos - _tailFill.BaseOffset;
            if (fillDir.magnitude > _borderThickness)
                _tailFill.TargetPoint = _tailFill.BaseOffset + fillDir - (fillDir.normalized * _borderThickness);
            else
                _tailFill.TargetPoint = _tailFill.BaseOffset;

            _tailOutline.SetAllDirty();
            _tailFill.SetAllDirty();
        }
    }
}