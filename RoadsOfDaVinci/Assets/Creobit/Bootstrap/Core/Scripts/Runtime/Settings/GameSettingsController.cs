using Cysharp.Threading.Tasks;
using VContainer;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Settings
{
    public sealed class GameSettingsController : IGameSettingsController
    {
        private GameSettingsService _gameSettingsService;
        private IObjectResolver _objectResolver;

        public GameSettings Settings => _gameSettingsService.Settings;

        [Inject]
        private void Construct(IObjectResolver objectResolver)
        {
            _objectResolver = objectResolver;
        }

        public UniTask Load()
        {
            _gameSettingsService = new();

            // Inject dependencies into the GameSettingsService using the object resolver.
            _objectResolver.Inject(_gameSettingsService);

            return _gameSettingsService.Load();
        }

        public void Dispose()
        {
            _gameSettingsService?.Dispose();
        }
    }
}