using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Dialogues.Core.Scripts.Runtime.Service;
using Creobit.Loading;
using Cysharp.Threading.Tasks;

using VContainer;

namespace Creobit.Dialogues.Core.Scripts.Runtime.Controller
{
    /// <summary>
    /// The DialogueController orchestrates the flow of conversational dialogues in the application.
    /// It provides mechanisms to start, advance, and end dialogues while managing relevant events and services.
    /// Implements the <see cref="ILoadUnit"/> interface to support loading functionality.
    /// </summary>
    public class DialogueController : IDialogueController
    {
        private IDialogueService _dialogueService;
        private IGameplayInputSystem _gameplayInputSystem;

        public event Action ConversationStarted;
        public event Action ConversationAdvanced;
        public event Action ConversationEnded;
        public event Action ConversationSkipped;

        public UniTask Load()
        {
            ConversationStarted += DisableInputOnDialogueStart;
            ConversationEnded += EnableInputOnDialogueEnd;
            ConversationSkipped += EnableInputOnDialogueEnd;
            return UniTask.CompletedTask;
        }

        [Inject]
        private void Construct(GameplaySceneReferences gameplaySceneReferences, 
            IReloadController reloadController,
            IGameplayInputSystem gameplayInputSystem)
        {
            _dialogueService = new DialogueService(gameplaySceneReferences,
                OnConversationStarted, OnConversationAdvanced, OnConversationSkipped, OnConversationEnded);
            _gameplayInputSystem = gameplayInputSystem;

            reloadController.AddReloadableObject(_dialogueService);
        }

        /// <summary>
        /// Starts the specified conversation using the dialogue system.
        /// </summary>
        /// <param name="conversation">The <see cref="dialogueID"/> representing the conversation to be started.</param>
        public void StartConversation(string dialogueID) => _dialogueService.StartConversation(dialogueID);

        /// <summary>
        /// Attempts to progress the current dialogue conversation.
        /// This method checks if advancing
        /// the conversation is possible via the configured dialogue service
        /// </summary>
        public void TryAdvanceConversation() => _dialogueService.TryAdvanceConversation();

        /// <summary>
        /// Ends the current active conversation within the dialogue system.
        /// </summary>
        public void EndCurrentConversation() => _dialogueService.EndCurrentConversation();

        private void OnConversationStarted() => ConversationStarted?.Invoke();
        private void OnConversationAdvanced() => ConversationAdvanced?.Invoke();
        private void OnConversationSkipped() => ConversationSkipped?.Invoke();
        private void OnConversationEnded() => ConversationEnded?.Invoke();

        private void EnableInputOnDialogueEnd()
        {
            _gameplayInputSystem.IsActionAvailable = true;
        }
        
        private void DisableInputOnDialogueStart()
        {
            //_gameplayInputSystem.IsActionAvailable = false;
        }
    }
}