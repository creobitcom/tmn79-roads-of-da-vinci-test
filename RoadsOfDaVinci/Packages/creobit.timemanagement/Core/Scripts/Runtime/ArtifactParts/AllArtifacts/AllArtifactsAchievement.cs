using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Types.AllArtifacts
{
    [CreateAssetMenu(menuName = "Create ArtifactsAchievement", fileName = "ArtifactsAchievement")]
    public class AllArtifactsAchievement : AchievementBase
    {
        public override AchievementObject GetAchievement()
        {
            return new AllArtifactsAchievementObject(AchievementData);
        }
    }
}