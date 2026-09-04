using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Google;
using Creobit.Dialogues.Core.Scripts.Editor.Utils;
using Creobit.Dialogues.Core.Scripts.Runtime;
using Creobit.Dialogues.Core.Scripts.Runtime.Utility;
using Creobit.Localization;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using ProjectType = Creobit.Localization.ProjectType;

namespace Creobit.Dialogues.Core.Scripts.Editor.ParserTools
{
    public class DialogueParserToolWindow : OdinEditorWindow
    {
        [MenuItem(RuntimeConstants.MenuItems.DialogueParserTool)]
        private static void OpenWindow()
        {
            var window = GetWindow<DialogueParserToolWindow>();
            
            window.position = GUIHelper.GetEditorWindowRect().AlignCenter(800, 150);
        }
        
        [SerializeField]
        private ProjectType _projectType;
        
        [SerializeField]
        [ShowIf("@_projectType == Creobit.Localization.Editor.ProjectType.Modules")]
        private string _tableID;
        
        [SerializeField]
        [ShowIf("@_projectType == Creobit.Localization.Editor.ProjectType.Toyman")]
        private string _localizationPath;
        
        
        private ParserConfig _parserConfig;
        
        private void OnBecameVisible()
        {
            if (!DialoguesEditorUtils.ConfigExists())
            {
                return;
            }

            LoadParserConfig();
        }

        private void LoadParserConfig()
        {
            _parserConfig =
                JsonConvert.DeserializeObject<ParserConfig>(File.ReadAllText(Path.Combine("Assets/GoogleSheets/ParsedConfigs/",RuntimeConstants.ParserData.ParserConfigFilePath)));

            _tableID = _parserConfig.TableId;
        }

        [Button]
        [HideIf(nameof(TableIDIsEmpty))]
        [ShowIf("@_projectType == Creobit.Localization.Editor.ProjectType.Modules")]
        // [HideIf(nameof(DialoguesEditorUtils.ConfigExists))]
        private void CreateConfig()
        {
            if (!Directory.Exists("Assets/GoogleSheets/ParsedConfigs/"))
            {
                Directory.CreateDirectory("Assets/GoogleSheets/ParsedConfigs/");
                
                AssetDatabase.Refresh();
            }

            var parserConfig = new ParserConfig()
            {
                TableId = _tableID,
            };

            File.WriteAllText(Path.Combine("Assets/GoogleSheets/ParsedConfigs/",RuntimeConstants.ParserData.ParserConfigFilePath), JsonConvert.SerializeObject(parserConfig));
            
            AssetDatabase.Refresh();
        }

        private bool TableIDIsEmpty()
        {
            return string.IsNullOrEmpty(_tableID);
        }

        [Button]
        [ShowIf("@_projectType == Creobit.Localization.Editor.ProjectType.Modules")]
        // [ShowIf(nameof(DialoguesEditorUtils.ConfigExists))]
        private void ParseGoogleSheets()
        {
            LoadParserConfig();
            var rows = GoogleSheetsService.ReadFromGoogleSheets(_parserConfig.TableId,
                RuntimeConstants.ParserData.SheetName);

            var dialogues = new List<Dialogue>();
            var bubbles = new List<Bubble>();

            Dialogue currentDialogue = null;
            Bubble currentBubble = null;

            for (var i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                if (IsRowEmpty(row)) continue;

                var rowType = row[0]?.ToString();
                switch (rowType)
                {
                    case "Dialogue":
                        currentDialogue = BeginNewDialogue(currentDialogue, ref bubbles, dialogues, row);
                        break;

                    case "Bubble":
                        currentBubble = BeginNewBubble(currentBubble, bubbles, row);
                        break;

                    case "Listener":
                        AddListenerToBubble(currentBubble, row);
                        break;

                    case "Line":
                        AddLineToBubble(currentBubble, row);
                        break;

                    case "Choice":
                        AddChoiceToLastLine(bubbles, row);
                        break;
                }
            }

            FinalizeDialogueAndBubble(bubbles, currentBubble, currentDialogue, dialogues);

            var conversationPack = new QuestPack
            {
                QuestID = RuntimeConstants.ParserData.SheetName,
                Dialogues = dialogues.ToArray()
            };
            File.WriteAllText(Path.Combine(Application.dataPath, RuntimeConstants.ParserData.DialogueJsonFilePath),
                JsonConvert.SerializeObject(conversationPack, Formatting.Indented));
            
            Debug.Log(JsonConvert.SerializeObject(conversationPack, Formatting.Indented));
        }

        [Button]
        [ShowIf("@_projectType == Creobit.Localization.Editor.ProjectType.Toyman")]
        private async UniTaskVoid ParseToyman()
        {
            var dialogues = new List<Dialogue>();
            var jsonContent = await new LocalizationFileHandler().LoadTextFileAsync(_localizationPath);
            var _localizationJson = JsonConvert.DeserializeObject<LocalizationJson>(jsonContent);

            var dialoguesRaw = _localizationJson.Data.Keys
                .Where(row => row.Contains("start_txt_level"));

            var dialoguesByLevels = new Dictionary<string, List<string>>();
            
            var charactersRaw = _localizationJson.Data.Keys
                .Where(row => row.Contains("char_info_level"));
            
            var charactersByLevels = new Dictionary<string, List<string>>();
            
            foreach (var dialogueRaw in dialoguesRaw)
            {
                var level = dialogueRaw.Replace("start_txt_level", "").Split("_")[0];
                dialoguesByLevels.TryAdd(level, new List<string>());
                dialoguesByLevels[level].Add(dialogueRaw);
            }
            
            foreach (var characterRaw in charactersRaw)
            {
                var level = characterRaw.Replace("char_info_level", "").Split("_")[0];
                charactersByLevels.TryAdd(level, new List<string>());
                charactersByLevels[level].Add(_localizationJson.Data[characterRaw]);
            }

            foreach (var level in dialoguesByLevels.Keys)
            {
                var bubbles = new List<Bubble>();

                for (var i = 0; i < dialoguesByLevels[level].Count; i++)
                {
                    var bubble = new Bubble
                    {
                        BubblePrefabID = i % 2 == 0 ? "PrefabGG10DialogueLeft" : "PrefabGG10DialogueRight",
                        Characters = new DialogueCharacter[]
                        {
                            new()
                            {
                                CharacterID = charactersByLevels[level][i].Trim(),
                                CharacterPlace = i % 2 == 0 ? "Left" : "Right",
                                CharacterState = "Neutral"
                            }
                        },
                        Lines = new DialogueLine[]
                        {
                            new()
                            {
                                Choices = Array.Empty<DialogueChoice>(),
                                GameTextID = dialoguesByLevels[level][i]
                            }
                        }
                    };
                    
                    bubbles.Add(bubble);
                }

                dialogues.Add(new Dialogue
                {
                    DialogueID = level,
                    Bubbles = bubbles.ToArray()
                });
            }

            var conversationPack = new QuestPack
            {
                QuestID = RuntimeConstants.ParserData.SheetName,
                Dialogues = dialogues.ToArray()
            };

            File.WriteAllText(Path.Combine(Application.streamingAssetsPath, 
                    RuntimeConstants.ParserData.DialogueJsonFilePath),
                JsonConvert.SerializeObject(conversationPack, Formatting.Indented));

            Debug.Log(JsonConvert.SerializeObject(conversationPack, Formatting.Indented));
        }

        private static bool IsRowEmpty(IList<object> row) => row.Count == 0;

        private static Dialogue BeginNewDialogue(Dialogue currentDialogue, ref List<Bubble> bubbles,
            List<Dialogue> dialogues, IList<object> row)
        {
            FinalizeDialogue(currentDialogue, bubbles, dialogues);
            bubbles = new List<Bubble>();
            return new Dialogue
            {
                DialogueID = row[(int)Entries.DialogueID]?.ToString(),
            };
        }

        private static void FinalizeDialogue(Dialogue currentDialogue, List<Bubble> bubbles, List<Dialogue> dialogues)
        {
            if (currentDialogue != null)
            {
                currentDialogue.Bubbles = bubbles.ToArray();
                dialogues.Add(currentDialogue);
            }
        }

        private static Bubble BeginNewBubble(Bubble currentBubble, List<Bubble> bubbles, IList<object> row)
        {
            if (currentBubble != null)
            {
                bubbles.Add(currentBubble);
            }

            return new Bubble
            {
                BubblePrefabID = row[(int)Entries.BubblePrefabID]?.ToString(),
                Characters = new[]
                {
                    CreateDialogueCharacter(row)
                },
                Lines = Array.Empty<DialogueLine>()
            };
        }

        private static void AddListenerToBubble(Bubble currentBubble, IList<object> row)
        {
            Assert.IsNotNull(currentBubble);

            Array.Resize(ref currentBubble.Characters, currentBubble.Characters.Length + 1);
            currentBubble.Characters[^1] = CreateDialogueCharacter(row);
        }

        private static void AddLineToBubble(Bubble currentBubble, IList<object> row)
        {
            Assert.IsNotNull(currentBubble);

            Array.Resize(ref currentBubble.Lines, currentBubble.Lines.Length + 1);
            currentBubble.Lines[^1] = new DialogueLine
            {
                GameTextID = row[(int)Entries.GameTextID]?.ToString(),
                Choices = Array.Empty<DialogueChoice>()
            };
        }

        private static void AddChoiceToLastLine(List<Bubble> bubbles, IList<object> row)
        {
            if (bubbles.Count == 0) return;

            var lastLine = bubbles[^1].Lines[^1];
            Array.Resize(ref lastLine.Choices, lastLine.Choices.Length + 1);
            lastLine.Choices[^1] = new DialogueChoice
            {
                GameTextID = row.Count > (int)Entries.GameTextID ? row[(int)Entries.GameTextID]?.ToString() : "",
                ChoicePrice = row.Count > (int)Entries.ChoicePrice
                    ? int.Parse(row[(int)Entries.ChoicePrice]?.ToString() ?? "0")
                    : 0,
                ChoiceCurrency = row.Count > (int)Entries.ChoiceCurrency
                    ? row[(int)Entries.ChoiceCurrency]?.ToString()
                    : ""
            };
        }

        private static void FinalizeDialogueAndBubble(List<Bubble> bubbles, Bubble currentBubble,
            Dialogue currentDialogue, List<Dialogue> dialogues)
        {
            if (currentBubble != null)
            {
                bubbles.Add(currentBubble);
            }

            FinalizeDialogue(currentDialogue, bubbles, dialogues);
        }

        //TODO : add correct row number in exception for easier times with google sheets
        private static DialogueCharacter CreateDialogueCharacter(IList<object> row)
        {
            if (row == null)
                throw new ArgumentNullException(nameof(row), "Row cannot be null");

            // Validate the required indices
            if (row.Count is <= (int)Entries.CharacterID or <= (int)Entries.CharacterPlace or <= (int)Entries.CharacterState)
                throw new ArgumentOutOfRangeException(nameof(row),
                    $"Fields CharacterID, CharacterPlace and CharacterState are required in google sheet. Row number is {row}");

            // Validate individual fields
            var characterID = row[(int)Entries.CharacterID]?.ToString();
            var characterPlace = row[(int)Entries.CharacterPlace]?.ToString();
            var characterState = row[(int)Entries.CharacterState]?.ToString();

            if (string.IsNullOrEmpty(characterID) ||
                string.IsNullOrEmpty(characterPlace) ||
                string.IsNullOrEmpty(characterState))
                throw new InvalidOperationException($"Row {row} has missing or invalid data in fields CharacterID, CharacterPlace and CharacterState for DialogueCharacter creation.");

            return new DialogueCharacter
            {
                CharacterID = characterID,
                CharacterPlace = characterPlace,
                CharacterState = characterState
            };
        }
        
        [Button]
        [ShowIf("@_projectType == Creobit.Localization.Editor.ProjectType.Modules")]
        // [ShowIf(nameof(DialoguesEditorUtils.ConfigExists))]
        private void ResetConfig()
        {
            AssetDatabase.DeleteAsset(Path.Combine("Assets/GoogleSheets/ParsedConfigs/",
                RuntimeConstants.ParserData.ParserConfigFilePath));
            
            AssetDatabase.Refresh();
        }
        
        private enum Entries
        {
            DialogueID = 4,
            BubblePrefabID = 6,
            CharacterID = 7,
            CharacterPlace = 8,
            CharacterState = 9,
            GameTextID = 10,
            ChoicePrice = 12,
            ChoiceCurrency = 13
        }

        [Serializable]
        [InlineProperty]
        [HideLabel]
        private class ParserConfig
        {
            [field: SerializeField]
            public string TableId { get; set; }
        }
    }
}