using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;

namespace Creobit.Dialogues.Core.Scripts.Runtime.Service
{
    public interface IDialogueService : IReloadable
    {
        public void StartConversation(string dialogueID);
        public bool TryAdvanceConversation();
        public bool EndCurrentConversation();
    }
}