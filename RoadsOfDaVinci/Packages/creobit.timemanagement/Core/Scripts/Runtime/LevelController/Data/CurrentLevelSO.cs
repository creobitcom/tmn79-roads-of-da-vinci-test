using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data
{
    [CreateAssetMenu(fileName = "CurrentLevel", menuName = "8floor/TimeManager/Levels/Current Level")]
    public class CurrentLevelSO : ScriptableObject, ITimeManagerSO
    {
        [field: SerializeField] public AssetReferenceT<LevelBaseSO> CurrentLevel {get; set;}
        [field: SerializeField] public bool WasVisited {get; set;}
        [field: SerializeField] public int LevelNum {get; set;}
    }
}