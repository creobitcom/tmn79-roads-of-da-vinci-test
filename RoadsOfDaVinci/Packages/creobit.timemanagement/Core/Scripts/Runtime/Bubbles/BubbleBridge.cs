using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Bubbles
{
    [Serializable]
    public class BubbleSequenceSetup
    {
        [SerializeField] private BubbleSequenceSO _sequence;
        [SerializeField] private UltEvent _onSequenceCompleted;

        public BubbleSequenceSO Sequence => _sequence;
        public UltEvent OnSequenceCompleted => _onSequenceCompleted;
    }

    public class BubbleBridge : MonoBehaviour
    {
        [Title("Sequence Events")] 
        [SerializeField] private List<BubbleSequenceSetup> _sequenceSetups = new();

        private IBubbleController _bubbleController;

        [Inject]
        public void Construct(IBubbleController bubbleController)
        {
            _bubbleController = bubbleController;
        }

        public void ShowBubbleSequence(BubbleSequenceSO sequenceToPlay, Transform anchor)
        {
            if (sequenceToPlay == null) return;

            var setup = _sequenceSetups.Find(x => x.Sequence == sequenceToPlay);

            if (setup != null)
            {
                _bubbleController.ShowSequence(sequenceToPlay, anchor, () => { setup.OnSequenceCompleted?.Invoke(); });
            }
            else
            {
                _bubbleController.ShowSequence(sequenceToPlay, anchor);
            }
        }

        public void ShowBubbleSequenceOnSelf(BubbleSequenceSO sequenceToPlay)
        {
            ShowBubbleSequence(sequenceToPlay, transform);
        }
    }
}