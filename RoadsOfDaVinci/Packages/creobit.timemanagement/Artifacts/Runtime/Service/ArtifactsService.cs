using System.Collections.Generic;
using _8floor.TimeManagement.Artifacts.Runtime.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Cysharp.Threading.Tasks;

namespace _8floor.TimeManagement.Artifacts.Runtime.Service
{
    public class ArtifactsService : IArtifactsService
    {
        private readonly IPlayerProfilesController _profilesController;
        
        private readonly List<ArtifactDataSO> _сachedAvailableArtifacts = new();

        public IReadOnlyList<ArtifactDataSO> Artifacts { get; private set; }
        
        public IReadOnlyList<ArtifactDataSO> AvailableArtifacts => _сachedAvailableArtifacts;
        
        private ArtifactsService(ArtifactsDataSO artifacts, IPlayerProfilesController profilesController)
        {
            Artifacts = artifacts.Artifacts;
         
            _profilesController = profilesController;
        }

        public UniTask Load()
        {
            foreach (var artifact in Artifacts)
            {
                if (ArtifactIsAvailable(artifact))
                {
                    _сachedAvailableArtifacts.Add(artifact);
                }
            }

            return UniTask.CompletedTask;
        }
        
        public bool PartIsReceived(string partName) => _profilesController.Service.CurrentProfile.ReceivedArtifactPartsNames.Contains(partName);
        
        public bool ArtifactIsAvailable(ArtifactDataSO artifact)
        {
            if (artifact.IsCollectorOnly)
            {
#if COLLECTOR
                return true;
#else
                return false;
#endif
            }

            return true;
        }
        
        public bool AllArtifactsIsCollected()
        {
            foreach (var artifact in Artifacts)
            {
                foreach (var part in artifact.Parts)
                {
                    if (!PartIsReceived(part.Name))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}