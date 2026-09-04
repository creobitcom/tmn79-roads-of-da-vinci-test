using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Loading;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ArtifactParts.Controller
{
    public interface IArtifactPartsController : ILoadUnit, IReloadable, IDisposable
    {
        public ArtifactPartsServiceBase Service { get; }
    }
}
