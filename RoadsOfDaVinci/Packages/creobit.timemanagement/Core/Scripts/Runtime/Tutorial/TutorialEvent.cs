using System;
using UltEvents;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial
{
    [Serializable]
    public class TutorialEvent
    {
        [field: SerializeField]
        public TutorialViewSo TutorialViewData { get; private set; }

        [field: SerializeField]
        public UltEvent AffectedTutorialEvent { get; private set; }
    }
}