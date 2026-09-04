using UnityEngine;

namespace Creobit.Dialogues.Core.Scripts.Runtime.View
{
    public class DialoguePanel : MonoBehaviour
    {
        [field: SerializeField] 
        public NovelDialoguePanel NpcPanel { get; private set; }

        [field: SerializeField] 
        public NovelDialoguePanel PlayerPanel { get; private set; }
    }
}