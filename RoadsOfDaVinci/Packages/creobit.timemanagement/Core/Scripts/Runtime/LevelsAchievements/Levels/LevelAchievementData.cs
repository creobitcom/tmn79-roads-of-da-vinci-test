using System;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Types.Levels
{
    [Serializable]
    public class LevelAchievementData
    {
        [field: SerializeField] 
        public UnityEngine.Rendering.SerializedDictionary<int, LevelData> levelsStars;

        public bool useGameMode;
        
        public void SetAllStarsCount(int starsCount)
        {
            foreach (var levelsStar in levelsStars.Values)
            {
                levelsStar.StarsCount = starsCount;
            }
        }
        
        [ShowIf("useGameMode")]
        [Button]
        public void SetAllGameMode(GameMode gameMode)
        {
            foreach (var levelsStar in levelsStars.Values)
            {
                levelsStar.BestGameMode = gameMode;
            }
        }
    }
}