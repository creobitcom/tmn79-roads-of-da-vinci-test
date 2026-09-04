using _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Types.Unique;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Types.Boosters
{
    [CreateAssetMenu(menuName = "Create BoosterAchievement", fileName = "BoosterAchievement")]
    public class BoosterAchievement : AchievementBase
    {
        [field: SerializeField] public string TargetBoosterId { get; private set; }

        public override AchievementObject GetAchievement()
        {
            return new BoosterAchievementObject(TargetBoosterId, AchievementData);
        }
    }
}