using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Types.Unique
{
    
    [CreateAssetMenu(menuName = "Create UniqueAchievement", fileName = "UniqueAchievement")]
    public class UniqueAchievement : AchievementBase
    {
        public override AchievementObject GetAchievement()
        {
            return new UniqueAchievementObject(AchievementData);
        }
    }
}