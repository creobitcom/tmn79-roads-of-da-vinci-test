using _8floor.TimeManagement.Core.Scripts.Runtime.Boosters;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements.Types.Boosters
{
    public class BoosterAchievementObject : AchievementObject
    {
        [Inject] protected IObjectResolver Resolver;

        private IBoostersController _boostersController;
        private readonly string _targetBoosterId;

        public BoosterAchievementObject(string targetBoosterId, AchievementData achievementData)
            : base(achievementData)
        {
            _targetBoosterId = targetBoosterId;
        }

        public override void Setup()
        {
            base.Setup();

            if (Resolver.TryResolve<IBoostersController>(out var boostersController))
            {
                _boostersController = boostersController;
                _boostersController.OnBoosterUsedEvent += OnBoosterUsed;
            }
        }

        public override void Dispose()
        {
            if (_boostersController != null)
            {
                _boostersController.OnBoosterUsedEvent -= OnBoosterUsed;
            }

            base.Dispose();
        }

        private void OnBoosterUsed(BoosterDataSO usedBooster)
        {
            if (usedBooster.Id == _targetBoosterId)
            {
                ChangeProgress(1);
            }
        }
    }
}