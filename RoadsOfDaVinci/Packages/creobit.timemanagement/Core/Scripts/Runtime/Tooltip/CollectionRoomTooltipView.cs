using Flexalon;
using TMPro;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip
{
    public class CollectionRoomTooltipView : MonoBehaviour, ITooltipView
    {
        [SerializeField] private FlexalonObject _flexalonObject;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _description;

        public Vector2 Size
        {
            get
            {
                var rect = (_flexalonObject.transform as RectTransform).rect;

                return new(rect.width, rect.height);
            }
        }
        public GameObject CurrentGameObject => gameObject;

        public void ForceUpdate()
        {
            _flexalonObject.ForceUpdate();
        }

        public void SetPosition(Vector2 position)
        {
            transform.localPosition = position;
        }

        public void SetTooltipText(string primaryText, string secondaryText)
        {
            _name.text = primaryText;
            _description.text = secondaryText;
        }
    }
}
