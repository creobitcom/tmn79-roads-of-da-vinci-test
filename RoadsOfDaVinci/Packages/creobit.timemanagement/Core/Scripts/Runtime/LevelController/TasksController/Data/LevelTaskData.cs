using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Data
{
    [Serializable]
    [HideLabel]
    public struct LevelTaskData
    {
        [field: SerializeField] 
        public string TaskName { get; set; }
        
        [field: SerializeField]
        public LevelTaskStatus TaskStatus { get; set; }
        
        [field: SerializeField]
        public ushort AmountToCompleteTask { get; set; }

        [field: SerializeField] 
        public bool IsShownOnStart { get; private set; }

        [field: SerializeField] 
        public bool IsBonusTask { get; private set; }
        
        [field: SerializeField] 
        public Color ProgressColor { get; private set; }
        
        [field: SerializeField] 
        public Sprite TaskIcon { get; private set; }
    }
}