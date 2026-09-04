using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using ObservableCollections;
using R3;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Types.Resources
{
    public class ResourceAchievementObject : AchievementObject
    {
        private readonly ResourceAchievementData _resourceAchievementData;

        [Inject] protected IObjectResolver Resolver;

        private IGameResourcesSystem _gameResourcesSystem;

        public ResourceAchievementObject(ResourceAchievementData resourceAchievementData, AchievementData achievementData)
            : base(achievementData)
        {
            _resourceAchievementData = resourceAchievementData;
        }

        public override void Setup()
        {
            base.Setup();

            if (AchievementStatus == AchievementStatus.Completed)
            {
                return;
            }

            if (!Resolver.TryResolve<IGameResourcesSystem>(out var gameResourcesSystem))
            {
                return;
            }

            _gameResourcesSystem = gameResourcesSystem;

            foreach (var resource in _resourceAchievementData.TrackedResources)
            {
                _gameResourcesSystem.CheckResourceIsLoaded(resource);
            }

            _gameResourcesSystem.Resources
                .ObserveReplace()
                .Subscribe(_ => TryCompleteAchievement())
                .AddTo(CompositeDisposable);

            if (_resourceAchievementData.IncludeInventoryResources)
            {
                _gameResourcesSystem.InventoryResources
                    .ObserveReplace()
                    .Subscribe(_ => TryCompleteAchievement())
                    .AddTo(CompositeDisposable);
            }

            if (Resolver.TryResolve<ILevelLoader>(out var levelLoader))
            {
                levelLoader.LevelBaseSO
                    .Skip(1)
                    .Subscribe(_ => TryCompleteAchievement())
                    .AddTo(CompositeDisposable);
            }

            TryCompleteAchievement();
        }

        private void TryCompleteAchievement()
        {
            if (AchievementStatus == AchievementStatus.Completed
                || _gameResourcesSystem == null
                || _resourceAchievementData.TrackedResources.Count == 0)
            {
                return;
            }

            var amounts = _resourceAchievementData.TrackedResources
                .Select(resource => _gameResourcesSystem.GetResourceAmount(resource))
                .ToArray();

            var conditionMet = _resourceAchievementData.Mode switch
            {
                ResourceAchievementMode.AllResources => amounts.All(amount => amount >= _resourceAchievementData.Threshold),
                ResourceAchievementMode.AnyResource => amounts.Any(amount => amount >= _resourceAchievementData.Threshold),
                _ => false
            };

            if (conditionMet)
            {
                ChangeProgress(1);
            }
        }
    }
}
