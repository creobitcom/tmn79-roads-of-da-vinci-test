using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.Actions;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.COC.UI
{
    public class CocActionView : MonoBehaviour
    {
        [field: SerializeField]
        public CocActionType ActionType { get; private set; }

        public event Action<CocActionType> OnButtonClick = delegate { };

        public void OnPointerClick(BaseEventData eventData)
        {
            if (((PointerEventData) eventData).button == PointerEventData.InputButton.Left) 
            {
                OnButtonClick?.Invoke(ActionType);
            }
        }
    }
}