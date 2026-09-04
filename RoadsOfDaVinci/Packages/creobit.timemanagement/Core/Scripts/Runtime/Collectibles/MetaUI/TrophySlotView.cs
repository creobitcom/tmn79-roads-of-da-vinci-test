using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles.MetaUI
{
    public class TrophySlotView : MonoBehaviour
    {
        [SerializeField] private GameObject _polaroidVisuals;
        [SerializeField] private Image _itemIcon;
        [SerializeField] private GameObject _lockedVisuals;
        [SerializeField] private Sprite _lockedSprite;

        public void SetEmpty()
        {
            if (_lockedVisuals != null)
            {
                _lockedVisuals.SetActive(false);
            }
            if (_polaroidVisuals != null)
            {
                _polaroidVisuals.SetActive(false);
            }
            if (_itemIcon != null)
            {
                _itemIcon.sprite = null;
            }
            gameObject.SetActive(false);
        }

        public void SetHidden()
        {
            SetEmpty();
        }

        public void SetLocked(Sprite fallbackLockedSprite = null)
        {
            gameObject.SetActive(true);

            if (_polaroidVisuals != null)
            {
                _polaroidVisuals.SetActive(true);
            }

            if (_lockedVisuals != null)
            {
                _lockedVisuals.SetActive(true);
            }

            if (_itemIcon != null)
            {
                var sprite = _lockedSprite != null ? _lockedSprite : fallbackLockedSprite;
                if (sprite != null)
                {
                    _itemIcon.sprite = sprite;
                    _itemIcon.gameObject.SetActive(true);
                }
                else if (_lockedVisuals == null)
                {
                    _itemIcon.gameObject.SetActive(false);
                }
            }
        }

        public void SetItem(Sprite iconSprite)
        {
            gameObject.SetActive(true);

            if (_lockedVisuals != null)
            {
                _lockedVisuals.SetActive(false);
            }

            if (_polaroidVisuals != null)
            {
                _polaroidVisuals.SetActive(true);
            }

            if (_itemIcon != null)
            {
                _itemIcon.sprite = iconSprite;
                _itemIcon.gameObject.SetActive(iconSprite != null);
            }
        }
    }
}
