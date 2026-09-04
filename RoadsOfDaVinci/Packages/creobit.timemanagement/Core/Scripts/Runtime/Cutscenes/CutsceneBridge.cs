using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes.Data;
using Cysharp.Threading.Tasks;
using UltEvents;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes
{
    [Serializable]
    public class CutsceneEventSetup
    {
        public CutsceneSequenceSO Cutscene;

        [Range(1, 99)] 
        public int CallsRequiredToPlay = 1;

        public bool ResetCountAfterPlay = false;

        public UltEvent OnCutsceneFinished;

        [HideInInspector] public int CurrentCallsCount = 0;
    }

    public class CutsceneBridge : MonoBehaviour
    {
        [SerializeField] private List<CutsceneEventSetup> _cutscenesOnLevel = new();

        private ICutsceneController _cutsceneController;

        [Inject]
        private void Construct(ICutsceneController cutsceneController)
        {
            _cutsceneController = cutsceneController;
        }

        public void PlayCutscene(CutsceneSequenceSO sequenceToPlay)
        {
            if (sequenceToPlay == null)
            {
                return;
            }

            var setup = _cutscenesOnLevel.Find(x => x.Cutscene == sequenceToPlay);

            if (setup == null)
            {
                _cutsceneController.PlayCutsceneAsync(sequenceToPlay).Forget();
                return;
            }

            setup.CurrentCallsCount++;

            if (setup.CurrentCallsCount >= setup.CallsRequiredToPlay)
            {
                if (setup.ResetCountAfterPlay)
                {
                    setup.CurrentCallsCount = 0;
                }

                _cutsceneController.PlayCutsceneAsync(setup.Cutscene, () => { setup.OnCutsceneFinished?.Invoke(); })
                    .Forget();
            }
        }

        public void ResetCounter(CutsceneSequenceSO sequence)
        {
            var setup = _cutscenesOnLevel.Find(x => x.Cutscene == sequence);
            if (setup != null)
            {
                setup.CurrentCallsCount = 0;
            }
        }
    }
}