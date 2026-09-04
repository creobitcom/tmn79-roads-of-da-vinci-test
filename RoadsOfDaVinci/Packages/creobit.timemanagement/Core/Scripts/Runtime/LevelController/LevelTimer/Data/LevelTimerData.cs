using System;
using System.Linq;
using System.Text;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer.Data
{
    [FoldoutGroup(RuntimeConstants.FoldoutNames.LevelTimerData)]
    [InlineProperty]
    [HideLabel]
    [Serializable]
    public class LevelTimerData : ISelfValidator
    {
        [field: SerializeField]
        [field: InlineProperty]
        [field: HideLabel]
        public GameplayIntervalGeneralParameters GameplayIntervalGeneralParameters { get; private set; }

        [field: SerializeField]
        public Orientation Orientation { get; private set; }

        [field: InfoBox("$StarsSummary")]
        [field: SerializeField]
        [field: RequiredListLength(3)]
        [field: TableList]
        public LevelStarData[] LevelStarsData { get; private set; }

        private float DurationSeconds => GameplayIntervalGeneralParameters == null
            ? 0f
            : GameplayIntervalGeneralParameters.DurationSeconds;

        private string StarsSummary
        {
            get
            {
                if (LevelStarsData == null || LevelStarsData.Length == 0)
                {
                    return "Звёзды не заданы.";
                }

                var duration = DurationSeconds;

                var builder = new StringBuilder();

                builder
                    .Append("В таблице — ПОТРАЧЕННОЕ время с начала уровня, а не остаток таймера. Длительность уровня ")
                    .Append(FormatSeconds(duration))
                    .Append(" (")
                    .Append(Mathf.RoundToInt(duration))
                    .Append(" сек):");

                var sorted = LevelStarsData
                    .Where(starData => starData != null)
                    .OrderBy(starData => starData.TimeLeftMoreThan)
                    .ToArray();

                for (var index = 0; index < sorted.Length; index++)
                {
                    var limit = sorted[index].TimeLeftMoreThan;

                    builder
                        .Append('\n')
                        .Append(sorted.Length - index)
                        .Append(" звезды: пройти за ")
                        .Append(FormatSeconds(limit))
                        .Append(" и быстрее (")
                        .Append(limit)
                        .Append(" сек)");

                    if (limit >= duration)
                    {
                        builder.Append("  <-- больше длительности уровня, эта звезда выдаётся всегда");
                    }
                }

                builder
                    .Append('\n')
                    .Append("Время идёт по реальным секундам от старта уровня; паузы и окна туториала не считаются.");

                return builder.ToString();
            }
        }

        public void Validate(SelfValidationResult result)
        {
            if (LevelStarsData == null)
            {
                return;
            }

            var duration = DurationSeconds;

            foreach (var starData in LevelStarsData)
            {
                if (starData == null || starData.TimeLeftMoreThan < duration)
                {
                    continue;
                }

                result.AddError($"Лимит звезды {starData.TimeLeftMoreThan} сек не меньше длительности уровня " +
                                $"({duration} сек) — такая звезда выдаётся всегда.");
            }
        }

        private static string FormatSeconds(float seconds)
        {
            var total = Mathf.Max(0, Mathf.RoundToInt(seconds));

            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}