using System.Collections.Generic;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements
{
    [CreateAssetMenu(menuName = "Create AchievementsData", fileName = "AchievementsData")]
    public class AchievementsData : ScriptableObject
    {
        public List<AchievementBase> achievements;
    }
}