using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Editor.Utils;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Sirenix.OdinInspector.Editor.Validation;
using UnityEditor;

[assembly: RegisterValidationRule(typeof(GameplayTagNameValidator))]

namespace _8floor.TimeManagement.Core.Scripts.Editor.Utils
{
    public class GameplayTagNameValidator : SceneValidator
    {
        protected override void Validate(ValidationResult result)
        {
            string[] assetsGuids = AssetDatabase.FindAssets($"t:{typeof(GameplayTagSO)}");

            HashSet<GameplayTagSO> tags = new HashSet<GameplayTagSO>();

            foreach (string guid in assetsGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                GameplayTagSO tag = AssetDatabase.LoadAssetAtPath<GameplayTagSO>(path);

                if (tags.Add(tag))
                {
                    continue;
                }

                result.Add(new ResultItem()
                {
                    Message = $"Detected duplication of GameplayTag with name: {tag.TagName}",
                    ResultType = ValidationResultType.Error,
                    SelectionObject = tag,
                    MetaData = null,
                });
            }
        }
    }
}
