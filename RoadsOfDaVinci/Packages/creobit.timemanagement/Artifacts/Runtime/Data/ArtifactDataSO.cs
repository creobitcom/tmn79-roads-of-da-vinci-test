using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace _8floor.TimeManagement.Artifacts.Runtime.Data
{
    [CreateAssetMenu(fileName = "ArtifactDataSO", menuName = "Creobit/Artifact/Create new ArtifactDataSO")]
    public class ArtifactDataSO : ScriptableObject
    {
        [SerializeField]
        private ArtifactPartDataSO[] _parts;

        [field: SerializeField]
        public bool IsCollectorOnly { get; private set; }

        [field: SerializeField]
        public string NameLocalizationKey { get; private set; }

        [field: SerializeField]
        public string DescriptionLocalizationKey { get; private set; }

        [field: SerializeField]
        public AssetReference Prefab { get; private set; }

        public IReadOnlyList<ArtifactPartDataSO> Parts => _parts;
    }
}