using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace _8floor.TimeManagement.Artifacts.Runtime.Data
{
    [CreateAssetMenu(fileName = "ArtifactPartDataSO", menuName = "Creobit/Artifact/Part/Create new ArtifactPartDataSO")]
    public class ArtifactPartDataSO : ScriptableObject
    {
        [ShowInInspector]
        [ReadOnly]
        [Tooltip("This name is taken from the asset file name.")]
        public string Name => name;

        [field: SerializeField]
        public AssetReferenceT<Sprite> PanelColoredSprite { get; private set; }

        [field: SerializeField]
        public AssetReferenceT<Sprite> PanelUnColoredSprite { get; private set; }

        [field: SerializeField]
        public AssetReferenceT<Sprite> ObjectColoredSprite { get; private set; }

        [field: SerializeField]
        public AssetReferenceT<Sprite> ObjectUnColoredSprite { get; private set; }

        [Button("Copy sprites to Object from Panel")]
        private void CopyPanelSprites()
        {
            ObjectColoredSprite = PanelColoredSprite;
            ObjectUnColoredSprite = PanelUnColoredSprite;
        }
    }
}