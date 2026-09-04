using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags
{
    [CreateAssetMenu(fileName = "GroupTags", menuName = "8floor/TimeManager/Gameplay/GroupTags")]
    public class GameplayGroupTagsSO : GameplayTagSO
    {
        [SerializeField] private GameplayTagSO[] _tags;
    }
}
