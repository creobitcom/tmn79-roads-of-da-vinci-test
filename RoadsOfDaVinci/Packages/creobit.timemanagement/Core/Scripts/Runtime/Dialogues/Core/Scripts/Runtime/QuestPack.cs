namespace Creobit.Dialogues.Core.Scripts.Runtime
{
    public class QuestPack
    {
        public string QuestID;
        public Dialogue[] Dialogues;
    }

    public class Dialogue
    {
        public string DialogueID;
        public Bubble[] Bubbles;
    }

    public class Bubble
    {
        public string BubblePrefabID;
        public DialogueCharacter[] Characters;
        public DialogueLine[] Lines;
    }

    public class DialogueCharacter
    {
        public string CharacterID;
        public string CharacterPlace;
        public string CharacterState;
    }

    public class DialogueLine
    {
        public string GameTextID;
        public DialogueChoice[] Choices;
    }

    public class DialogueChoice
    {
        public string GameTextID;
        public int ChoicePrice;
        public string ChoiceCurrency;
    }
}