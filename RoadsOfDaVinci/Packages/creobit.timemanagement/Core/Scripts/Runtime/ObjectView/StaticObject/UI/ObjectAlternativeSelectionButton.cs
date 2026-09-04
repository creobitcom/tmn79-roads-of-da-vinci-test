using System;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.UI
{
    public class ObjectAlternativeSelectionButton : MonoBehaviour
    {
        private static readonly Color DisabledTint = new(0.5f, 0.5f, 0.5f, 1f);

        [SerializeField] private Image _iconImage;
        [SerializeField] private Button _button;

        private event Action<int> _onClickCallback;
        private int _index;
        private Sprite _icon;
        private Sprite _disabledIcon;
        private bool _canAfford;
        private bool _interactionBlocked;

        public int Index => _index;

        public void Initialize(Sprite icon, int index, Action<int> onClick)
        {
            Initialize(icon, null, true, index, onClick);
        }

        public void Initialize(Sprite icon, Sprite disabledIcon, bool canAfford, int index, Action<int> onClick)
        {
            if (_iconImage == null) _iconImage = GetComponent<Image>();
            if (_button == null) _button = GetComponent<Button>();

            _icon = icon;
            _disabledIcon = disabledIcon;
            _canAfford = canAfford;
            _interactionBlocked = false;

            RefreshVisual();

            _index = index;
            _onClickCallback = onClick;

            _button.onClick.RemoveListener(OnButtonClicked);
            _button.onClick.AddListener(OnButtonClicked);
        }

        /// <summary>
        /// Перерисовать серую/обычную картинку под новую доступность, не пересоздавая кнопку.
        /// Нужно, пока панель уже висит на экране: ресурсы могли приехать (юнит донёс до базы)
        /// или уйти (их потратили на другой объект) уже ПОСЛЕ того, как панель собрали.
        /// </summary>
        public void SetAffordable(bool canAfford)
        {
            if (_canAfford == canAfford)
            {
                return;
            }

            _canAfford = canAfford;

            RefreshVisual();
        }

        public void SetInteractionBlocked(bool blocked)
        {
            if (_interactionBlocked == blocked)
            {
                return;
            }

            _interactionBlocked = blocked;

            RefreshVisual();
        }

        private void RefreshVisual()
        {
            ApplyVisual(_icon, _disabledIcon, _canAfford && !_interactionBlocked);
        }

        private void ApplyVisual(Sprite icon, Sprite disabledIcon, bool canAfford)
        {
            if (_iconImage == null)
            {
                return;
            }

            if (canAfford)
            {
                if (icon != null)
                {
                    _iconImage.sprite = icon;
                }

                _iconImage.color = Color.white;
                return;
            }

            if (disabledIcon != null)
            {
                _iconImage.sprite = disabledIcon;
                _iconImage.color = Color.white;
                return;
            }

            if (icon != null)
            {
                _iconImage.sprite = icon;
            }

            _iconImage.color = DisabledTint;
        }

        private void OnButtonClicked()
        {
            if (_interactionBlocked)
            {
                return;
            }

            _onClickCallback?.Invoke(_index);
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnButtonClicked);
            }

            _onClickCallback = null;
        }
    }
}
