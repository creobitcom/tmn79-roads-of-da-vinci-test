using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Controller;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Types.AllAchievements
{
    public class AllAchievementsAchievementObject : AchievementObject
    {
        protected readonly AchievementData _achievementData;

        [Inject]
        protected IAchievementsController AchievementsController;

        public AllAchievementsAchievementObject(AchievementData achievementData)
            : base(achievementData)
        {
            _achievementData = achievementData;
        }

        public override void Setup()
        {
            base.Setup();
            if (AchievementsController != null)
            {
                AchievementsController.OnCompleteAchievement += TryCompleteAchievement;
            }
        }

        public override void Dispose()
        {
            if (AchievementsController != null)
            {
                AchievementsController.OnCompleteAchievement -= TryCompleteAchievement;
            }
            base.Dispose();
        }

        private void TryCompleteAchievement(AchievementBase achievementObject)
        {
            if (AchievementsController.Achievements.Values.All(achievement =>
                    achievement.AchievementStatus == AchievementStatus.Completed
                    || achievement is AllAchievementsAchievementObject))
            {
                ChangeProgress(1);
            }
        }
    }
}