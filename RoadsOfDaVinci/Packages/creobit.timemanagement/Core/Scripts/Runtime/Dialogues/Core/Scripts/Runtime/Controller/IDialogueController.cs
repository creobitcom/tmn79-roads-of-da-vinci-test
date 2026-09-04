using System;
using Creobit.Loading;

namespace Creobit.Dialogues.Core.Scripts.Runtime.Controller
{
    public interface IDialogueController : ILoadUnit
    {
        public event Action ConversationStarted;
        public event Action ConversationAdvanced;
        public event Action ConversationEnded;
        public event Action ConversationSkipped;

        public void StartConversation(string dialogueID);
        public void TryAdvanceConversation();
        public void EndCurrentConversation();
    }
}