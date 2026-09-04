using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Artifacts.Runtime.Data
{
    [CreateAssetMenu(fileName = "ArtifactsDataSO", menuName = "Creobit/Artifact/Create new ArtifactsDataSO")]
    public class ArtifactsDataSO : ScriptableObject, ISelfValidator
    {
        [SerializeField]
        private ArtifactDataSO[] _artifacts;

        public IReadOnlyList<ArtifactDataSO> Artifacts => _artifacts;

        public void Validate(SelfValidationResult result)
        {
            Dictionary<ArtifactPartDataSO, ArtifactDataSO> parts = new();

            foreach (var artifact in _artifacts)
            {
                foreach (var part in artifact.Parts)
                {
                    if (!parts.TryAdd(part, artifact))
                    {
                        result.AddError($"Part {part.Name} already exists in artifact {artifact.name}");
                        return;
                    }
                }
            }
        }
    }
}