using Creobit.Dialogues.Core.Scripts.Runtime.Controller;
using UnityEngine;
using VContainer;

namespace Creobit.Dialogues.Core.Scripts.Runtime.Bridge
{
    public class DialogueBridge : MonoBehaviour
    {
        IDialogueController _dialogueController;

        [Inject]
        private void Construct(IDialogueController dialogueController)
        {
            _dialogueController = dialogueController;
        }

        public void StartDialogue(string dialogueID)
        {
            _dialogueController.StartConversation(dialogueID);
        }

        public void TryAdvanceDialogue()
        {
            _dialogueController.TryAdvanceConversation();
        }

        public void EndDialogue()
        {
            _dialogueController.EndCurrentConversation();
        }
    }
}
