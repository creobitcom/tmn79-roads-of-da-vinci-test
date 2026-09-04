using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Types.Resources
{
    [CreateAssetMenu(menuName = "Create ResourceAchievement", fileName = "ResourceAchievement")]
    public class ResourceAchievement : AchievementBase
    {
        [field: SerializeField] public ResourceAchievementData ResourceAchievementData { get; private set; }

        public override AchievementObject GetAchievement()
        {
            return new ResourceAchievementObject(ResourceAchievementData, AchievementData);
        }
    }
}
