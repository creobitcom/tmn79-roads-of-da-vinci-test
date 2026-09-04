namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Types.Unique
{
    public class UniqueAchievementObject : AchievementObject
    {
        private readonly AchievementData _achievementData;

        public UniqueAchievementObject(AchievementData achievementData)
            : base(achievementData)
        {
            _achievementData = achievementData;
        }
    }
}