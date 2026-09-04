using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView
{
    [Serializable]
    public class UnitTypeCount
    {
        public GameplayTagsContainsMode tagMode;
        public GameplayTagSO[] unitType;
        public int count;
    }
}