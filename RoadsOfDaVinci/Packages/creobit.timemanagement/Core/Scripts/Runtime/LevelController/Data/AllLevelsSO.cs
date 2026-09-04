using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data
{
    [CreateAssetMenu(fileName = "AllLevels", menuName = "8floor/TimeManager/Levels/All Levels")]
    public class AllLevelsSO : ScriptableObject, ITimeManagerSO
    {
        [field: SerializeField]
        public List<LevelReferenceByNum> AllLevels {get; set;}

        [field: SerializeField]
        public bool LimitLevels { get; private set; }

        [field: SerializeField]
        public int MaxLevel { get; private set; } = 10;

        [field: SerializeField]
        public int MainCampaignLevels { get; private set; }

        public int MaxCampaignLevel
        {
            get
            {
                var maxLevels = AllLevels?.Count ?? 0;

                if (LimitLevels)
                {
                    maxLevels = Mathf.Min(maxLevels, MaxLevel);
                }

#if !COLLECTOR
                if (MainCampaignLevels > 0)
                {
                    maxLevels = Mathf.Min(maxLevels, MainCampaignLevels);
                }
#endif

                return maxLevels;
            }
        }

        public bool IsLevelAvailable(int levelNum)
        {
            return levelNum <= MaxCampaignLevel;
        }

        public bool IsCollectibleAvailable(CollectibleItemSO item)
        {
            if (item == null || AllLevels == null)
            {
                return true;
            }

            var maxCampaignLevel = MaxCampaignLevel;

            for (var i = 0; i < AllLevels.Count; i++)
            {
                var level = AllLevels[i];

                if (level.MapTrophy != item && level.MapArtifact != item)
                {
                    continue;
                }

                return level.levelNum <= maxCampaignLevel;
            }

            return true;
        }
    }
}