using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.Dialogues.Core.Scripts.Runtime.View
{
    public class CharacterPanel : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _characterName;
        
        [SerializeField]
        private Image _characterImage;
        
        public void SetCharacter(CharacterData conversationCharacter)
        {
            _characterName.text = conversationCharacter.DialogueCharacter.CharacterID;
            _characterImage.sprite = conversationCharacter.CharacterIcon;
        }
        
        public void SetPanelState(bool state)
        {
            gameObject.SetActive(state);
        }
    }
}