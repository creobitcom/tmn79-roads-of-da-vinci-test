using Cysharp.Threading.Tasks;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ArtifactParts.Controller
{
    public sealed class ArtifactPartsController : IArtifactPartsController
    {
        private IObjectResolver _objectResolver;
        private ArtifactPartsService _service;

        public ArtifactPartsServiceBase Service => _service;

        [Inject]
        private void Construct(IObjectResolver objectResolver)
        {
            _objectResolver = objectResolver;
        }

        public UniTask Load()
        {
            _service = new ArtifactPartsService();

            // Inject dependencies into the PlayerProfilesController using the object resolver.
            _objectResolver.Inject(_service);

            return UniTask.CompletedTask;
        }

        public UniTask Reload() => _service.Reload();

        public void Dispose() => _service.Dispose();
    }

    public sealed class ArtifactPartsControllerMeta : IArtifactPartsController
    {
        private IObjectResolver _objectResolver;
        private ArtifactPartsServiceMeta _service;

        public ArtifactPartsServiceBase Service => _service;

        [Inject]
        private void Construct(IObjectResolver objectResolver)
        {
            _objectResolver = objectResolver;
        }

        public UniTask Reload()
        {
            return UniTask.CompletedTask;
        }

        public UniTask Load()
        {
            _service = new ArtifactPartsServiceMeta();

            // Inject dependencies into the PlayerProfilesController using the object resolver.
            _objectResolver.Inject(_service);

            return UniTask.CompletedTask;
        }

        public void Dispose() => _service.Dispose();
    }
}
