using _8floor.TimeManagement.Artifacts.Runtime.Service;
using _8floor.TimeManagement.Core.Scripts.Runtime.ArtifactParts.Controller;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Types.AllArtifacts
{
    public class AllArtifactsAchievementObject : AchievementObject
    {
        protected readonly AchievementData _achievementData;

        [Inject]
        protected IArtifactsService ArtifactsService;

        [Inject]
        protected IArtifactPartsController ArtifactPartsController;

        public AllArtifactsAchievementObject(AchievementData achievementData)
            : base(achievementData)
        {
            _achievementData = achievementData;
        }

        public override void Setup()
        {
            base.Setup();
            ArtifactPartsController.Service.PartCollected += TryCompleteAchievement;
        }

        private void TryCompleteAchievement()
        {
            if (ArtifactsService.AllArtifactsIsCollected())
            {
                ChangeProgress(1);
            }
        }
    }
}