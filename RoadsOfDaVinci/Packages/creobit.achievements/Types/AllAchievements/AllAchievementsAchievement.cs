using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Types.AllAchievements
{
    [CreateAssetMenu(menuName = "Create AllAchievementsAchievement", fileName = "AllAchievementsAchievement")]
    public class AllAchievementsAchievement : AchievementBase
    {
        public override AchievementObject GetAchievement()
        {
            return new AllAchievementsAchievementObject(AchievementData);
        }
    }
}