using System;
using Creobit.Dialogues.Core.Scripts.Runtime.Controller;
using Cysharp.Threading.Tasks;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Initialization.GameplayInit
{
    public class DisabledDialogueController : IDialogueController
    {
        public event Action ConversationStarted
        {
            add { }
            remove { }
        }

        public event Action ConversationAdvanced
        {
            add { }
            remove { }
        }

        public event Action ConversationEnded
        {
            add { }
            remove { }
        }

        public event Action ConversationSkipped
        {
            add { }
            remove { }
        }

        public UniTask Load() => UniTask.CompletedTask;

        public void StartConversation(string dialogueID)
        {
        }

        public void TryAdvanceConversation()
        {
        }

        public void EndCurrentConversation()
        {
        }
    }
}
