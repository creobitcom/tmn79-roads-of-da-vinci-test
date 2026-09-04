using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial.Animations
{
    /// <summary>
    /// Одна анимация в наборе. Все шаги набора играют параллельно, каждый со своей задержкой.
    /// </summary>
    [Serializable]
    public struct TutorialAnimationStep
    {
        [HideLabel]
        [EnumToggleButtons]
        public TutorialAnimationKind Kind;

        [LabelText("Длительность")]
        [SuffixLabel("сек", true)]
        [MinValue(0f)]
        [Tooltip("0 — взять общую длительность набора.")]
        public float Duration;

        [LabelText("Задержка")]
        [SuffixLabel("сек", true)]
        [MinValue(0f)]
        public float Delay;

        [LabelText("Плавность")]
        public Ease Ease;

        [ShowIf(nameof(Kind), TutorialAnimationKind.Move)]
        [LabelText("Откуда прилетает")]
        public TutorialAnimationDirection Direction;

        [ShowIf(nameof(Kind), TutorialAnimationKind.Move)]
        [LabelText("Расстояние")]
        [SuffixLabel("px", true)]
        public float Distance;

        [ShowIf(nameof(Kind), TutorialAnimationKind.Scale)]
        [LabelText("Начальный масштаб")]
        [Tooltip("Меньше 1 — окно вырастает, больше 1 — схлопывается.")]
        public float FromScale;

        [ShowIf(nameof(Kind), TutorialAnimationKind.Rotate)]
        [LabelText("Угол")]
        [SuffixLabel("°", true)]
        public float Angle;

        [ShowIf(nameof(Kind), TutorialAnimationKind.Punch)]
        [LabelText("Сила")]
        public float Strength;

        // Значения по умолчанию для тех полей, которые дизайнер не трогал. Структура в List<>
        // создаётся как default(T) — все нули, поэтому «пустой» шаг обязан анимировать что-то
        // осмысленное, а не превращаться в мгновенный скачок.
        public const float DefaultDistance = 80f;
        public const float DefaultFromScale = 0.85f;
        public const float DefaultAngle = 12f;
        public const float DefaultStrength = 0.25f;

        public float EffectiveDistance => Mathf.Approximately(Distance, 0f) ? DefaultDistance : Distance;

        public float EffectiveFromScale => Mathf.Approximately(FromScale, 0f) ? DefaultFromScale : FromScale;

        public float EffectiveAngle => Mathf.Approximately(Angle, 0f) ? DefaultAngle : Angle;

        public float EffectiveStrength => Mathf.Approximately(Strength, 0f) ? DefaultStrength : Strength;

        public float GetDuration(float fallbackDuration) => Duration > 0f ? Duration : fallbackDuration;

        public Vector2 GetOffset()
        {
            var distance = EffectiveDistance;

            return Direction switch
            {
                TutorialAnimationDirection.Left => new Vector2(-distance, 0f),
                TutorialAnimationDirection.Right => new Vector2(distance, 0f),
                TutorialAnimationDirection.Up => new Vector2(0f, distance),
                TutorialAnimationDirection.Down => new Vector2(0f, -distance),
                _ => Vector2.zero,
            };
        }

        public static TutorialAnimationStep Create(TutorialAnimationKind kind, Ease ease = Ease.OutQuad)
        {
            return new TutorialAnimationStep
            {
                Kind = kind,
                Ease = ease,
                Distance = DefaultDistance,
                FromScale = DefaultFromScale,
                Angle = DefaultAngle,
                Strength = DefaultStrength,
            };
        }
    }
}
