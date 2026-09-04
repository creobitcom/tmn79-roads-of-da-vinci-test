using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial;
using Creobit.Dialogues.Core.Scripts.Runtime.Controller;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using R3;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Mediators
{
    /// <summary>
    /// Mediator that manages the interaction between dialogue and tutorial systems.
    /// </summary>
    public class DialogueTutorialsMediator : ILoadUnit, IReloadable, IDisposable
    {
        private IDisposable _levelStartStateListener;

        private ILevelLoader _levelLoader;
        private ILevelController _levelController;
        private IDialogueController _dialogueController;
        private ITutorialController _tutorialController;

        public DialogueTutorialsMediator(ILevelLoader levelLoader, ILevelController levelController, IDialogueController dialogueController, ITutorialController tutorialController, IReloadController reloadController)
        {
            _levelLoader = levelLoader;
            _levelController = levelController;
            _dialogueController = dialogueController;
            _tutorialController = tutorialController;

            reloadController.AddReloadableObject(this);
        }

        public UniTask Load()
        {
            _levelStartStateListener = _levelController.IsLevelStarted.Subscribe(OnLevelStartedValueChanged);

            _levelController.DialogueStarted += ShowLevelDialogue;

            return UniTask.CompletedTask;
        }

        public UniTask Reload()
        {
            Dispose();

            return Load();
        }

        public void Dispose()
        {
            _levelStartStateListener.Dispose();

            _levelController.DialogueStarted -= ShowLevelDialogue;

            _dialogueController.ConversationSkipped -= ShowLevelPressAnythingObject;
            _dialogueController.ConversationEnded -= ShowLevelPressAnythingObject;
        }

        /// <summary>
        /// Handles the event when the level start state changes.
        /// </summary>
        /// <param name="levelIsStarted">Indicates whether the level has started.</param>
        /// <remarks>
        /// This method is triggered when the level's start status changes.
        /// If the level has started, it initiates a predefined dialogue sequence 
        /// and subscribes to the conversation end and skip events to handle the tutorial sequence.
        /// </remarks>
        private void OnLevelStartedValueChanged(bool levelIsStarted)
        {
            if (!levelIsStarted)
            {
                return;
            }

            var levelData = _levelLoader.LevelBaseSO.CurrentValue;
            if (levelData.UsingStartTutorial && levelData.StartTutorial != null)
            {
                _tutorialController.ShowTutorial(levelData.StartTutorial);
            }
        }

        private void ShowLevelDialogue()
        {
            var levelData = _levelLoader.LevelBaseSO.CurrentValue;
            // уважаем флаг UsingStartDialogue (как UsingStartTutorial выше): если выключен или id пуст —
            // не стартуем диалог, иначе StartConversation выставит _currentDialogue без панели → NRE при reload
            if (!levelData.UsingStartDialogue || string.IsNullOrEmpty(levelData.StartDialogue))
            {
                return;
            }

            _dialogueController.StartConversation(levelData.StartDialogue);

            _dialogueController.ConversationSkipped += ShowLevelPressAnythingObject;
            _dialogueController.ConversationEnded += ShowLevelPressAnythingObject;
        }

        private void ShowLevelPressAnythingObject()
        {
            _levelController.SetPressAnythingToContinueObjectState(true);
        }
    }
}