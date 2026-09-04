using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using TMPro;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameResources
{
    [Serializable]
    public struct WorkerResourceByTag
    {
        public GameplayTagSO tag;
        public ResourceBaseSO worker;
    }
}