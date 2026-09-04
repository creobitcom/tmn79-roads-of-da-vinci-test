using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial
{
    public class TutorialHudRaycaster : GraphicRaycaster
    {
        private readonly List<RaycastResult> _buffer = new();

        public bool IsFiltering { get; set; }

        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            if (!IsFiltering)
            {
                base.Raycast(eventData, resultAppendList);
                return;
            }

            _buffer.Clear();
            base.Raycast(eventData, _buffer);

            for (var index = 0; index < _buffer.Count; index++)
            {
                var result = _buffer[index];

                if (result.gameObject == null)
                {
                    continue;
                }

                if (result.gameObject.GetComponentInParent<TutorialHudPassthrough>(true) != null)
                {
                    resultAppendList.Add(result);
                }
            }

            _buffer.Clear();
        }
    }
}
