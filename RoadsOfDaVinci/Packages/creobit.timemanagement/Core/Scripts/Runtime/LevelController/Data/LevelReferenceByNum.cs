using System;
using _8floor.TimeManagement.Artifacts.Runtime.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data
{
    [Serializable]
    public struct LevelReferenceByNum
    {
        public int levelNum;
        public AssetReferenceT<LevelBaseSO> level;
        public int locationNum;
        public AssetReferenceT<Sprite> guide;

        public ArtifactPartDataSO ArtifactPartDataSO;
        public CollectibleItemSO MapTrophy;
        public CollectibleItemSO MapArtifact;
    }
}