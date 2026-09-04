using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using VContainer;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller
{
    public sealed class PlayerProfilesController : IPlayerProfilesController
    {
        private IObjectResolver _objectResolver;

        public PlayerProfilesRulesData Rules { get; private set; }
        public PlayerProfilesService Service { get; private set; }
        public void AddServicesToLoadWith<T>(T param, params ILoadUnit<T>[] loadUnits)
        {
            throw new System.NotImplementedException();
        }

        [Inject]
        private void Construct(IObjectResolver objectResolver)
        {
            _objectResolver = objectResolver;
        }

        public UniTask Load(PlayerProfilesRulesData playerProfilesData)
        {
            Rules = playerProfilesData;
            
            Service = new();

            // Inject dependencies into the PlayerProfilesController using the object resolver.
            _objectResolver.Inject(Service);

            return Service.Load(playerProfilesData);
        }

        public void Dispose()
        {
            Service?.Dispose();
        }
    }
}