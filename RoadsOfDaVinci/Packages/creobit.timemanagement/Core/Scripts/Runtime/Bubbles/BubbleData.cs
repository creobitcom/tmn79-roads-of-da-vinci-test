using System;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Bubbles
{
    [Serializable]
    public class BubbleData
    {
        [TextArea]
        [SerializeField] private string _textLocalizationKey;
        [SerializeField] private Vector2 _offset = new(0, 150);
        [SerializeField] private Vector2 _tailOffset = Vector2.zero;
        [SerializeField] private float _displayDuration = 0f;
        [SerializeField] private bool _isPausingGame = true;
        [SerializeField] private bool _isClosedByClick = true;
        [SerializeField] private bool _isSkipButtonUsed = false;
        [SerializeField] private bool _isMovable = false;

        public string TextLocalizationKey => _textLocalizationKey;
        public Vector2 Offset => _offset;
        public Vector2 TailOffset => _tailOffset;
        public float DisplayDuration => _displayDuration;
        public bool IsPausingGame => _isPausingGame;
        public bool IsClosedByClick => _isClosedByClick;
        public bool IsSkipButtonUsed => _isSkipButtonUsed;
        public bool IsMovable => _isMovable;
    }
}