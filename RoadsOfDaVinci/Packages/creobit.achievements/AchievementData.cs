using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements
{
    [Serializable]
    [HideLabel]
    public class AchievementData
    {
        [field: SerializeField] 
        public string AchievementName { get; set; }
        
        [field: SerializeField]
        public ushort AmountToCompleteAchievement { get; set; }
        
        [field: SerializeField]
        public Sprite AchievementIcon { get; set; } 
    }
}