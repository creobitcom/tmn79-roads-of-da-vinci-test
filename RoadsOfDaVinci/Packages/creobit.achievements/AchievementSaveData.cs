using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.Achievements;
using UnityEngine.SocialPlatforms;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Achievements
{
    [Serializable]
    public class AchievementSaveData
    {
        public string AchievementName;
        public AchievementStatus AchievementStatus;

        public AchievementSaveData(string achievementName, string achievementStatus)
        {
            AchievementName = achievementName;
            AchievementStatus = Enum.Parse<AchievementStatus>(achievementStatus, true);
        }
    }
}