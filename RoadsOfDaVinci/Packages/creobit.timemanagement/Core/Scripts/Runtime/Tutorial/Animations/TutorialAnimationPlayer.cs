using DG.Tweening;
using DG.Tweening.Core.Easing;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial.Animations
{
    /// <summary>
    /// Проигрывает набор анимаций. Вся математика собрана в <see cref="Sample"/> — и рантайм,
    /// и предпросмотр в редакторе идут через него, поэтому в редакторе видно ровно то же,
    /// что будет в игре.
    /// </summary>
    public static class TutorialAnimationPlayer
    {
        private const float EaseOvershoot = 1.70158f;

        /// <summary>
        /// Исходное состояние элемента: то, как он выглядит на префабе.
        /// Появление всегда заканчивается в нём, исчезновение — начинается из него.
        /// </summary>
        public readonly struct HomeState
        {
            public readonly Vector2 AnchoredPosition;
            public readonly Vector3 LocalScale;
            public readonly Vector3 LocalEulerAngles;

            public HomeState(Vector2 anchoredPosition, Vector3 localScale, Vector3 localEulerAngles)
            {
                AnchoredPosition = anchoredPosition;
                LocalScale = localScale;
                LocalEulerAngles = localEulerAngles;
            }

            public static HomeState Capture(RectTransform rect) => new(
                rect.anchoredPosition,
                rect.localScale,
                rect.localEulerAngles);
        }

        /// <summary>
        /// Возвращает элемент в исходное состояние. Вызывать перед показом, иначе остатки
        /// прошлого скрытия (прозрачность 0, съехавшая позиция) утекут в новый показ.
        /// </summary>
        public static void ResetToHome(RectTransform rect, CanvasGroup canvasGroup, HomeState home)
        {
            if (rect != null)
            {
                rect.anchoredPosition = home.AnchoredPosition;
                rect.localScale = home.LocalScale;
                rect.localEulerAngles = home.LocalEulerAngles;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        /// <summary>
        /// Применяет состояние набора на момент времени <paramref name="time"/> (в секундах от начала).
        /// Чистая функция без DOTween — годится и для предпросмотра в редакторе.
        /// </summary>
        public static void Sample(TutorialAnimationSet set, float time, RectTransform rect,
            CanvasGroup canvasGroup, HomeState home, bool isShow)
        {
            if (set == null || set.IsEmpty || rect == null)
            {
                return;
            }

            foreach (var step in set.Steps)
            {
                var duration = step.GetDuration(set.DefaultDuration);
                var progress = GetProgress(step.Delay, duration, time);

                ApplyStep(step, EvaluateEase(step, progress), rect, canvasGroup, home, isShow);
            }
        }

        /// <summary>
        /// Запускает набор в рантайме. Возвращает null, если анимировать нечего.
        /// </summary>
        public static Tween Build(TutorialAnimationSet set, RectTransform rect, CanvasGroup canvasGroup,
            HomeState home, bool isShow)
        {
            if (set == null || set.IsEmpty || rect == null)
            {
                return null;
            }

            var total = set.TotalDuration;

            if (total <= 0f)
            {
                Sample(set, float.MaxValue, rect, canvasGroup, home, isShow);
                return null;
            }

            // Нулевой кадр применяем сразу: иначе окно успеет мигнуть в конечном состоянии
            // до первого тика твина.
            Sample(set, 0f, rect, canvasGroup, home, isShow);

            return DOVirtual
                .Float(0f, total, total, time => Sample(set, time, rect, canvasGroup, home, isShow))
                .SetEase(Ease.Linear)
                // Независимо от Time.timeScale: окно туториала может показываться на паузе уровня.
                .SetUpdate(true);
        }

        private static float GetProgress(float delay, float duration, float time)
        {
            if (duration <= 0f)
            {
                return time >= delay ? 1f : 0f;
            }

            return Mathf.Clamp01((time - delay) / duration);
        }

        private static float EvaluateEase(TutorialAnimationStep step, float progress)
        {
            // Ease.Unset — значение по умолчанию у только что добавленного шага.
            var ease = step.Ease == Ease.Unset ? Ease.OutQuad : step.Ease;

            return EaseManager.Evaluate(ease, null, progress, 1f, EaseOvershoot, 0f);
        }

        private static void ApplyStep(TutorialAnimationStep step, float eased, RectTransform rect,
            CanvasGroup canvasGroup, HomeState home, bool isShow)
        {
            switch (step.Kind)
            {
                case TutorialAnimationKind.Fade:
                {
                    if (canvasGroup == null)
                    {
                        return;
                    }

                    canvasGroup.alpha = isShow ? Mathf.Clamp01(eased) : Mathf.Clamp01(1f - eased);
                    return;
                }

                case TutorialAnimationKind.Scale:
                {
                    var from = home.LocalScale * step.EffectiveFromScale;

                    // Unclamped, чтобы кривые с перелётом (OutBack) реально перелетали.
                    rect.localScale = isShow
                        ? Vector3.LerpUnclamped(from, home.LocalScale, eased)
                        : Vector3.LerpUnclamped(home.LocalScale, from, eased);
                    return;
                }

                case TutorialAnimationKind.Move:
                {
                    var from = home.AnchoredPosition + step.GetOffset();

                    rect.anchoredPosition = isShow
                        ? Vector2.LerpUnclamped(from, home.AnchoredPosition, eased)
                        : Vector2.LerpUnclamped(home.AnchoredPosition, from, eased);
                    return;
                }

                case TutorialAnimationKind.Rotate:
                {
                    var from = home.LocalEulerAngles + new Vector3(0f, 0f, step.EffectiveAngle);

                    rect.localEulerAngles = isShow
                        ? Vector3.LerpUnclamped(from, home.LocalEulerAngles, eased)
                        : Vector3.LerpUnclamped(home.LocalEulerAngles, from, eased);
                    return;
                }

                case TutorialAnimationKind.Punch:
                {
                    // Затухающая пружинка: три качка, сходящих на нет к концу.
                    var wave = Mathf.Sin(eased * Mathf.PI * 3f) * (1f - Mathf.Clamp01(eased));

                    rect.localScale = home.LocalScale * (1f + step.EffectiveStrength * wave);
                    return;
                }
            }
        }
    }
}
