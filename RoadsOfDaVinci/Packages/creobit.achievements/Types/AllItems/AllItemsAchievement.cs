using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Types.AllItems
{
    [CreateAssetMenu(menuName = "Create ItemsAchievement", fileName = "ItemsAchievement")]
    public class AllItemsAchievement : AchievementBase
    {
        public override AchievementObject GetAchievement()
        {
            return new AllItemsAchievementObject(AchievementData);
        }
    }
}