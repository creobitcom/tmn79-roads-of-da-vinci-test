using _8floor.TimeManagement.Artifacts.Runtime.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ArtifactParts.Data
{
    [System.Serializable]
    public class GameplayArtifactPartData
    {
        [field: SerializeField]
        public GameplayIntervalGeneralParameters ShowArtifactPartInterval { get; private set; }

        [field: SerializeField]
        public GameplayIntervalGeneralParameters ActiveArtifactPartInterval { get; private set; }

        [field: SerializeField]
        public ArtifactPartDataSO ArtifactPart { get; private set; }
    }
}