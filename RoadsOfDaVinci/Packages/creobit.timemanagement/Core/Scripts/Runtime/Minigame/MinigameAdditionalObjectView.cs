using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using R3;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Minigame
{
    public class MinigameAdditionalObjectView : StaticObjectView
    {
        [FoldoutGroup(RuntimeConstants.FoldoutNames.Minigame)]
        [SerializeField] private List<MinigameAdditionalObjectStage> stages;
        
        [FoldoutGroup(RuntimeConstants.FoldoutNames.Minigame)]
        [SerializeField] private int currentStage;
        
        public string currentKey;
        public event Action OnKeyChanged;

        public void Init()
        {
            currentKey = stages[currentStage].key;
            stages[currentStage].onKeySwitched?.Invoke();
        }
        
        public void SetNextKey()
        {
            currentStage = (currentStage + 1) % stages.Count;
            currentKey = stages[currentStage].key;
            OnKeyChanged?.Invoke();
            stages[currentStage].onKeySwitched?.Invoke();
        }
    }
}