using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial.Animations
{
    /// <summary>
    /// Набор анимаций для одного события (появление или исчезновение). Шаги комбинируются:
    /// играют одновременно, каждый со своей задержкой и длительностью.
    /// Пустой набор = мгновенное появление/исчезновение, как было до появления анимаций.
    /// </summary>
    [Serializable]
    public class TutorialAnimationSet
    {
        [LabelText("Общая длительность")]
        [SuffixLabel("сек", true)]
        [MinValue(0f)]
        [Tooltip("Используется теми шагами, у которых длительность не задана.")]
        [SerializeField]
        private float defaultDuration = 0.25f;

        [LabelText("Шаги")]
        [ListDrawerSettings(ShowFoldout = false, CustomAddFunction = nameof(CreateDefaultStep))]
        [SerializeField]
        private List<TutorialAnimationStep> steps = new();

        public float DefaultDuration => defaultDuration;

        public IReadOnlyList<TutorialAnimationStep> Steps => steps;

        public bool IsEmpty => steps == null || steps.Count == 0;

        /// <summary>
        /// Сколько всего длится набор: самый поздний шаг задаёт конец.
        /// </summary>
        public float TotalDuration
        {
            get
            {
                if (IsEmpty)
                {
                    return 0f;
                }

                var total = 0f;

                foreach (var step in steps)
                {
                    total = Mathf.Max(total, step.Delay + step.GetDuration(defaultDuration));
                }

                return total;
            }
        }

        public void Set(float duration, params TutorialAnimationStep[] newSteps)
        {
            defaultDuration = duration;
            steps = new List<TutorialAnimationStep>(newSteps ?? Array.Empty<TutorialAnimationStep>());
        }

        public void Clear()
        {
            steps = new List<TutorialAnimationStep>();
        }

        private TutorialAnimationStep CreateDefaultStep() =>
            TutorialAnimationStep.Create(TutorialAnimationKind.Fade);

        #region Пресеты

        public static TutorialAnimationStep[] PresetFade() => new[]
        {
            TutorialAnimationStep.Create(TutorialAnimationKind.Fade),
        };

        public static TutorialAnimationStep[] PresetGrow() => new[]
        {
            TutorialAnimationStep.Create(TutorialAnimationKind.Fade),
            TutorialAnimationStep.Create(TutorialAnimationKind.Scale),
        };

        public static TutorialAnimationStep[] PresetFrom(TutorialAnimationDirection direction)
        {
            var move = TutorialAnimationStep.Create(TutorialAnimationKind.Move);
            move.Direction = direction;

            return new[]
            {
                TutorialAnimationStep.Create(TutorialAnimationKind.Fade),
                move,
            };
        }

        public static TutorialAnimationStep[] PresetBounce()
        {
            var scale = TutorialAnimationStep.Create(TutorialAnimationKind.Scale, Ease.OutBack);
            scale.FromScale = 0.6f;

            return new[]
            {
                TutorialAnimationStep.Create(TutorialAnimationKind.Fade),
                scale,
            };
        }

        #endregion
    }
}
