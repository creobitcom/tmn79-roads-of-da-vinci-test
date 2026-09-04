using System;
using System.Collections.Generic;
using Creobit.Loading;
using UnityEngine.SocialPlatforms.Impl;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Controller
{
    public interface IAchievementsController : ILoadUnit<AchievementsData>
    {
        public event Action<AchievementBase> OnCompleteAchievement;
        public IReadOnlyDictionary<AchievementBase, AchievementObject> Achievements { get; }
        public void ChangeAchievementProgress(AchievementBase achievementToChange, short amount);
        public void LoadAchievement(AchievementBase achievement);

    }
}