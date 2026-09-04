using UnityEngine;
using UnityEngine.Serialization;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Types.Levels
{
    [CreateAssetMenu(menuName = "Create LevelsAchievement", fileName = "LevelsAchievement")]
    public class LevelsAchievement : AchievementBase
    {
        [field: SerializeField] 
        public LevelAchievementData levelAchievementData;
        
        public override AchievementObject GetAchievement()
        {
            return new LevelsAchievementObject(levelAchievementData, AchievementData);
        }
    }
}