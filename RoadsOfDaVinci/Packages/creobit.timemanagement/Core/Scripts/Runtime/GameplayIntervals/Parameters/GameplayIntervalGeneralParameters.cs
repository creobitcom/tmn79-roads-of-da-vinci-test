using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters
{
    [Serializable]
    public class GameplayIntervalGeneralParameters : ISelfValidator
    {
        [MinValue(RuntimeConstants.Intervals.AccuracySeconds)]
        [SerializeField]
        private float _durationSeconds;
        
        [MinValue(RuntimeConstants.Intervals.AccuracySeconds)]
        [SerializeField]
        private float _tickIntervalSeconds;
        
        [field: ShowIf(nameof(ShowLoopAmountsInEditor))]
        [field: Tooltip("Amount of loops (-1 is infinite)")]
        [field: MinValue(-1)]
        [field: SerializeField]
        public int LoopAmounts { get; private set; }

        public bool ShowLoopAmountsInEditor { get; set; } = true;

        public float DurationSeconds { get => _durationSeconds; set => _durationSeconds = value; }

        public GameplayIntervalGeneralParameters()
        {
        }

        public GameplayIntervalGeneralParameters(float durationSeconds, float tickIntervalSeconds, int loopAmounts)
        {
            _durationSeconds = durationSeconds;
            _tickIntervalSeconds = tickIntervalSeconds;
            LoopAmounts = loopAmounts;
        }

        public bool IsInfiniteLoops => LoopAmounts == -1;

        public int DurationMilliseconds => (int)(_durationSeconds * 1000);
        
        public int TickIntervalMilliseconds => (int)(_tickIntervalSeconds * 1000);
        
        [ShowInInspector]
        private string _durationTime
        {
            get
            {
                TimeSpan timeSpan = TimeSpan.FromMilliseconds(DurationMilliseconds);

                return $"min:{timeSpan.Minutes:D2}: sec:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds:D2}";
            }
        }

        [ShowInInspector]
        private string _tickIntervalTime
        {
            get
            {
                TimeSpan timeSpan = TimeSpan.FromMilliseconds(TickIntervalMilliseconds);

                return $"min:{timeSpan.Minutes:D2}: sec:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds:D2}";
            }
        }

        public void Validate(SelfValidationResult result)
        {
            if (LoopAmounts == 0)
            {
                result.AddError("LoopAmounts cant be equal 0");
            }

            if (DurationMilliseconds % RuntimeConstants.Intervals.AccuracyMilliseconds != 0)
            {
                result.AddError($"Value {nameof(DurationMilliseconds)} does not match step {RuntimeConstants.Intervals.AccuracyMilliseconds}");
            }
            
            if (TickIntervalMilliseconds % RuntimeConstants.Intervals.AccuracyMilliseconds != 0)
            {
                result.AddError($"Value {nameof(TickIntervalMilliseconds)} does not match step {RuntimeConstants.Intervals.AccuracyMilliseconds}");
            }

            if (TickIntervalMilliseconds >= DurationMilliseconds)
            {
                result.AddError($"{nameof(TickIntervalMilliseconds)} cant be equal or greater than {nameof(DurationMilliseconds)}");
            }
        }
    }
}
