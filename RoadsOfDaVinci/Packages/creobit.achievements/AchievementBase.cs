using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements
{
    public abstract class AchievementBase : ScriptableObject
    {
        [field: SerializeField] 
        [field: InlineProperty]
        public virtual AchievementData AchievementData { get; private set; }
        
        public abstract AchievementObject GetAchievement();
    }
}