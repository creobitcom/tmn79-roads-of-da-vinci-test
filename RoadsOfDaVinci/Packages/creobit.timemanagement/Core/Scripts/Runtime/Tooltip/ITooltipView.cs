using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip
{
    public interface ITooltipView
    {
        public Vector2 Size { get; }

        public GameObject CurrentGameObject { get; }

        public void SetPosition(Vector2 position);

        public void SetTooltipText(string primaryText, string secondaryText);

        public void ForceUpdate();
    }
}
