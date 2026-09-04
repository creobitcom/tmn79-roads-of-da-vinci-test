using System;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.COC.UI
{
    public class CocView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _timeText;
        
        [SerializeField]
        private TMP_Text _workersText;
        
        [SerializeField]
        private Image _cocImage;
        
        public event Action<CocView> OnButtonClick;

        public event Action<CocView> OnButtonOver;

        public StaticObjectView TransitionToRef { get; private set; }

        public void SetData(StaticObjectView objectView, BuildingSettings buildingSettings)
        {
            TransitionToRef = objectView;
            
            _workersText.text = $"W: {buildingSettings.UnitTypeCounts.Sum(typeCount => typeCount.count)}";
        }

        public void SetResourceData(ResourceAmount[] resourceAmounts)
        {
            
        }

        public void SetTime(TransitionData intervalData)
        {
            _timeText.text = $"T: {(int)(intervalData.GameplayIntervalGeneralParameters.DurationMilliseconds * 0.001f)}";
        }

        public void OnPointerClick(BaseEventData baseEventData) 
        {
            if (((PointerEventData) baseEventData).button == PointerEventData.InputButton.Left) 
            {
                OnButtonClick?.Invoke(this);
            }
        }

        public void OnPointerOver(BaseEventData baseEventData) 
        {
            OnButtonOver?.Invoke(this);
        }
    }
}