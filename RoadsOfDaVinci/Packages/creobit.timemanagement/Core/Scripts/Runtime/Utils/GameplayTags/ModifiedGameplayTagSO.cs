using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags
{
    [CreateAssetMenu(fileName = "ModifiedGameplayTagSO", menuName = "8floor/TimeManager/Gameplay/ModifiedGameplayTagSO")]
    public class ModifiedGameplayTagSO : GameplayTagSO
    {
        [field: FoldoutGroup("Interaction Modificator")]
        [field: SerializeField]
        public bool ShowInteractionModificator { get; set; }

        [field: FoldoutGroup("Interaction Modificator")]
        [field: SerializeField]
        [field: ShowIf(nameof(ShowInteractionModificator))]
        public int InteractionModificator { get; private set; }
    }
}