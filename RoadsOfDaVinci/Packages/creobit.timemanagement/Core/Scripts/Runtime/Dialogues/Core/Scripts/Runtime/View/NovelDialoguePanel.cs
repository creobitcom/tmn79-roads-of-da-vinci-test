using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.Dialogues.Core.Scripts.Runtime.View
{
    public interface IDialoguePanel
    {
        public void SetDialogueData(DialogueData dialogueData);
        public void SetPanelState(bool state);
    }

    public interface IDialogueChoicePanel
    {
        public void SetDialogueChoiceData(DialogueChoiceData dialogueChoiceData);
    }

    public class DialogueData
    {
        public string DialogueText { get; set; }
        
        public CharacterData[] Characters { get; set; }
    }

    public class DialogueChoiceData
    {
        public DialogueChoice[] Choices { get; set; }
    }

    public class CharacterData
    {
        public Sprite CharacterIcon { get; set; }
        public DialogueCharacter DialogueCharacter { get; set; }
    }

    public class NovelDialoguePanel : MonoBehaviour, IDialoguePanel
    {
        [field: SerializeField]
        public TMP_Text DialogueText { get; private set; }
        
        [field: SerializeField]
        public CharacterPanel[] CharacterPanels { get; private set; }
        
        [field: SerializeField]
        public Button AdvanceConversationButton { get; private set; }

        public void SetDialogueData(DialogueData dialogueData)
        {
            DeactivateCharacterPanels();
            
            DialogueText.text = dialogueData.DialogueText;

            for (var i = 0; i < dialogueData.Characters.Length; i++)
            {
                CharacterPanels[i].SetCharacter(dialogueData.Characters[i]);
                
                CharacterPanels[i].SetPanelState(true);
            }
        }

        public void SetPanelState(bool state)
        {
            gameObject.SetActive(state);
        }

        private void DeactivateCharacterPanels()
        {
            foreach (var characterPanel in CharacterPanels)
            {
                characterPanel.SetPanelState(false);
            }
        }
    }
}