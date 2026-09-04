using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Bubbles
{
    [CreateAssetMenu(fileName = "BubbleSequence", menuName = "8floor/TimeManager/Bubbles/Bubble Sequence")]
    public class BubbleSequenceSO : ScriptableObject, ITimeManagerSO
    {
        [SerializeField] private List<BubbleData> _bubbles = new();

        public IReadOnlyList<BubbleData> Bubbles => _bubbles;

        public void InitializeForRuntime(List<BubbleData> bubbles)
        {
            _bubbles = bubbles;
        }
    }
}