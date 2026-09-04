using System;
using System.Collections.Generic;
using System.IO;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Dialogues.Core.Scripts.Runtime.Controller;
using Creobit.Dialogues.Core.Scripts.Runtime.Utility;
using Creobit.Dialogues.Core.Scripts.Runtime.View;
using Creobit.Localization;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Creobit.Dialogues.Core.Scripts.Runtime.Service
{
    /// <summary>
    /// DialogueService is responsible for handling the logic of dialogue interactions in the application.
    /// It provides functionality to start a conversation, advance through dialogue lines, and end the current conversation.
    /// </summary>
    public class DialogueService : IDialogueService, IReloadable
    {
        private readonly Dictionary<string, CharacterDataSO> _charactersDataSO = new();

        private DialoguePanelService _dialoguePanelService;

        private Dialogue _currentDialogue;
        private IDialoguePanel _currentDialoguePanel;
        private int _currentLineIndex = -1;
        private int _currentBubbleIndex;
        private QuestPack _questPack;

        private GameplaySceneReferences _gameplaySceneReferences;

        private Action _conversationStarted;
        private Action _conversationAdvanced;
        private Action _conversationSkipped;
        private Action _conversationEnded;

        public DialogueService(GameplaySceneReferences gameplaySceneReferences,
            Action conversationStarted,
            Action conversationAdvanced,
            Action conversationSkipped,
            Action conversationEnded)
        {
#if UNITY_STANDALONE
            _questPack = JsonConvert.DeserializeObject<QuestPack>(System.IO.File.ReadAllText(Path.Combine
            (Application.streamingAssetsPath,
                RuntimeConstants.ParserData
                    .DialogueJsonFilePath))); // streaming assets not for production! (use addressables)
#else
            LoadAndroidDialogues().Forget();
#endif

            _dialoguePanelService = new DialoguePanelService();
            _gameplaySceneReferences = gameplaySceneReferences;

            if (_dialoguePanelService._dialoguePanels.Count > 0)
            {
                _dialoguePanelService._dialoguePanels.Add("PrefabGG10DialogueLeft",
                    _gameplaySceneReferences.DialoguePanels[0]);
                _dialoguePanelService._dialoguePanels.Add("PrefabGG10DialogueRight",
                    _gameplaySceneReferences.DialoguePanels[1]);
            }
            

            _conversationStarted = conversationStarted;
            _conversationAdvanced = conversationAdvanced;
            _conversationSkipped = conversationSkipped;
            _conversationEnded = conversationEnded;

            foreach (var characterData in gameplaySceneReferences.CharactersDataSO)
            {
                _charactersDataSO.Add(characterData.ID, characterData);
            }
        }

        private async UniTask LoadAndroidDialogues()
        {
            var filePath = Path.Combine(Application.streamingAssetsPath, RuntimeConstants.ParserData.DialogueJsonFilePath);

            var request = UnityWebRequest.Get(filePath);
            await request.SendWebRequest();

            if (request.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
            {
                return;
            }       

            var jsonText = request.downloadHandler.text;
            _questPack = JsonConvert.DeserializeObject<QuestPack>(jsonText);
        }

        public UniTask Reload()
        {
            if (_currentDialogue != null)
            {
                // панель может быть null, если диалог "стартовал", но GetDialoguePanel не нашёл
                // bubble-панель по BubblePrefabID (диалоги выключены / контент части не настроен)
                _currentDialoguePanel?.SetPanelState(false);

                _currentDialogue = null;
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Starts a conversation within the dialogue system using the specified dialogue dialogue.
        /// This initializes the dialogue panel and prepares it for displaying dialogue content.
        /// </summary>
        /// <param name="dialogue">The <see cref="Bubble"/> containing dialogue details, such as prefab ID, characters, and lines, to initiate the conversation.</param>
        public void StartConversation(string dialogueID)
        {
            foreach (var dialogue in _questPack.Dialogues)
            {
                if (dialogue.DialogueID == dialogueID)
                {
                    _currentDialogue = dialogue;
                    break;
                }
            }

            if (_currentDialogue == null)
            {
                Debug.LogError($"Dialogue with ID {dialogueID} not found.");
                return;
            }

            _currentBubbleIndex = 0;
            _currentDialoguePanel = _dialoguePanelService.GetDialoguePanel(_currentDialogue.Bubbles[_currentBubbleIndex].BubblePrefabID);

            _currentLineIndex = -1;

            _currentDialoguePanel.SetPanelState(true);

            if (!TryAdvanceConversation())
            {
                return;
            }

            _conversationStarted?.Invoke();
        }

        /// <summary>
        /// Attempts to advance the current conversation to the next dialogue line.
        /// </summary>
        /// <remarks>
        /// This method progresses the active conversation by moving to the next line in the current bubble's dialogue sequence.
        /// If the end of the conversation is reached, it terminates the conversation.
        /// </remarks>
        /// <returns>
        /// true if the conversation successfully advances to the next dialogue line;
        /// false if the conversation has concluded or no further progression is possible.
        /// </returns>
        public bool TryAdvanceConversation()
        {
            // Check if we're already at the end of all bubbles
            if (IsAtEndOfDialogue())
            {
                EndCurrentConversation();
                return false;
            }

            // Move to the next line
            _currentLineIndex++;

            // Check if we need to move to the next bubble
            if (IsAtEndOfCurrentBubble())
            {
                // Move to the next bubble
                _currentBubbleIndex++;

                // Check if we've reached the end of all bubbles
                if (IsAtEndOfDialogue())
                {
                    EndCurrentConversation();
                    return false;
                }

                // Setup the new bubble's panel
                SwitchToNextBubblePanel();
                _currentLineIndex = 0;
            }

            // Display the current line
            DisplayCurrentLine();

            _conversationAdvanced?.Invoke();

            return true;
        }

        private bool IsAtEndOfDialogue()
        {
            return _currentBubbleIndex >= _currentDialogue.Bubbles.Length;
        }

        private bool IsAtEndOfCurrentBubble()
        {
            return _currentLineIndex >= _currentDialogue.Bubbles[_currentBubbleIndex].Lines.Length;
        }

        private void SwitchToNextBubblePanel()
        {
            _currentDialoguePanel.SetPanelState(false);
            _currentDialoguePanel =
                _dialoguePanelService.GetDialoguePanel(_currentDialogue.Bubbles[_currentBubbleIndex].BubblePrefabID);
            _currentDialoguePanel.SetPanelState(true);
        }

        private void DisplayCurrentLine()
        {
            var currentBubble = _currentDialogue.Bubbles[_currentBubbleIndex];
            var currentDialogueLine = currentBubble.Lines[_currentLineIndex];
            _currentDialoguePanel.SetDialogueData(GetLocalizedDialogueData(currentDialogueLine.GameTextID,
                currentBubble.Characters));
        }

        /// <summary>
        /// Ends the current active conversation.
        /// </summary>
        /// <returns>
        /// Returns true if there are remaining dialogue lines to process in the conversation;
        /// otherwise, returns false.
        /// </returns>
        public bool EndCurrentConversation()
        {
            _currentDialoguePanel.SetPanelState(false);

            bool result = _currentLineIndex < _currentDialogue.Bubbles[0].Lines.Length;

            if (!result)
            {
                _conversationSkipped?.Invoke();
            }
            else
            {
                _conversationEnded?.Invoke();
            }

            return result;
        }

        // private DialogueData GetLocalizedDialogueData(string gameTextId, DialogueCharacter[] conversationCharacters)
        private DialogueData GetLocalizedDialogueData(string gameTextId, DialogueCharacter[] conversationCharacters)
        {
            var localizedDialogueData = new DialogueData();
            var localizedConversationCharacters = Array.Empty<CharacterData>();

            localizedDialogueData.DialogueText = LocalizationService.Instance.GetText(gameTextId);
            // localizedDialogueData.DialogueText = gameTextId;

            foreach (var conversationCharacter in conversationCharacters)
            {
                if (!_charactersDataSO.TryGetValue(conversationCharacter.CharacterID, out var characterDataSO))
                {
                    throw new KeyNotFoundException($"Character with ID '{conversationCharacter.CharacterID}' not found in character data.");
                }

                if (!characterDataSO.Icons.TryGetValue(conversationCharacter.CharacterState, out var icon))
                {
                    throw new KeyNotFoundException($"Icon for character state '{conversationCharacter.CharacterState}' not found for character ID '{conversationCharacter.CharacterID}'.");
                }

                var localizedCharacterData = new CharacterData()
                {
                    DialogueCharacter = new DialogueCharacter()
                    {
                        // CharacterID = _localizationController.GetLocalizedText(conversationCharacter.CharacterID),
                        CharacterID = conversationCharacter.CharacterID,
                        CharacterState = conversationCharacter.CharacterState,
                        CharacterPlace = conversationCharacter.CharacterPlace,
                    },

                    CharacterIcon = icon,
                };

                Array.Resize(ref localizedConversationCharacters, localizedConversationCharacters.Length + 1);

                localizedConversationCharacters[^1] = localizedCharacterData;
            }

            localizedDialogueData.Characters = localizedConversationCharacters;

            return localizedDialogueData;
        }
    }
}