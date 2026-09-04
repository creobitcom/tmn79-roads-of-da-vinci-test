using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using VContainer;
using Cysharp.Threading.Tasks;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Types.Levels
{
    public class LevelsAchievementObject : AchievementObject
    {
        protected readonly LevelAchievementData _achievementData;

        [Inject]
        protected IObjectResolver Resolver;
        
        [Inject]
        protected ISaveController SaveController;

        protected ILevelController LevelController;

        public LevelsAchievementObject(LevelAchievementData levelAchievementData, AchievementData achievementData)
            : base(achievementData)
        {
            _achievementData = levelAchievementData;
        }

        public override void Setup()
        {
            base.Setup();
            
            if (Resolver.TryResolve<ILevelController>(out var levelController))
            {
                LevelController = levelController;
                LevelController.LevelFinished += TryCompleteAchievement;
            }

            TryCompleteAchievement(null);
        }

        public override void Dispose()
        {
            base.Dispose();
            if (LevelController != null)
            {
                LevelController.LevelFinished -= TryCompleteAchievement;
            }
        }
        
        private async void TryCompleteAchievement(LevelBaseSO level)
        {
            if (level != null)
            {
                await UniTask.Yield();
            }

            if (SaveController?.CurrentSaveData?.Levels == null) 
            {
                return;
            }

            short matchingLevelsCount = 0;

            foreach (var levelsStar in _achievementData.levelsStars)
            {
                int levelId = levelsStar.Key;
                
                if (SaveController.CurrentSaveData.Levels.TryGetValue(levelId, out var levelSaveData))
                {
                    bool isPassedStars = levelSaveData.StarsCount >= levelsStar.Value.StarsCount;
                    bool isPassedGameMode = !_achievementData.useGameMode || 
                                            levelSaveData.BestGameMode >= levelsStar.Value.BestGameMode;

                    if (isPassedStars && isPassedGameMode)
                    {
                        matchingLevelsCount++;
                    }
                }
            }
            

            short difference = (short)(matchingLevelsCount - CurrentAmount);

            if (difference > 0)
            {
                ChangeProgress(difference);
            }
        }
    }
}