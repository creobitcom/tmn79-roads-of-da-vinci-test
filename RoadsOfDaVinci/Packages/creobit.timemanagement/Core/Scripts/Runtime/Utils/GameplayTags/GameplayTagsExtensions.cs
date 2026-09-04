using System;
using System.Collections.Generic;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags
{
    public static class GameplayTagsExtensions 
    {
        /// <summary>
        /// Determines if the original collection <paramref name="tags"/> contains tags from the <paramref name="otherTags"/> collection based on the <paramref name="containsMode"/>.
        /// </summary>
        /// <param name="tags">The original collection of gameplay tags.</param>
        /// <param name="containsMode">The mode that specifies how to check for the presence of tags (e.g., All or OnlyOne).</param>
        /// <param name="otherTags">The collection of gameplay tags to check against the original tags collection.</param>
        /// <returns>True if the tags in <paramref name="otherTags"/> match the condition specified by <paramref name="containsMode"/>; otherwise, false.</returns>
        public static bool Contains(this GameplayTagSO[] tags, GameplayTagsContainsMode containsMode, GameplayTagSO[] otherTags)
        {
            if (otherTags.Length == 0)
            {
                return false;
            }

            return containsMode switch
            {
                GameplayTagsContainsMode.All => ContainsAll(tags, otherTags),
                GameplayTagsContainsMode.OnlyOne => ContainsOnlyOne(tags, otherTags),
                _ => throw new ArgumentOutOfRangeException(nameof(containsMode), $"Unexpected value: {containsMode}.")
            };
        }
        
        private static bool ArrayContainsTag(GameplayTagSO tag, GameplayTagSO[] otherTags)
        {
            foreach (var otherTag in otherTags)
            {
                if (tag.Equals(otherTag))
                {
                    return true;
                }
            }

            return false;
        }
        
        private static bool ContainsAll(GameplayTagSO[] tags, GameplayTagSO[] otherTags)
        {
            foreach (var otherTag in otherTags)
            {
                if (!ArrayContainsTag(otherTag, tags))
                {
                    return false;
                }
            }
            
            return true;
        }
        
        private static bool ContainsOnlyOne(GameplayTagSO[] tags, ICollection<GameplayTagSO> otherTags)
        {
            foreach (var otherTag in otherTags)
            {
                if (ArrayContainsTag(otherTag, tags))
                {
                    return true;
                }
            }
            
            return false;
        }
        
    }
}
