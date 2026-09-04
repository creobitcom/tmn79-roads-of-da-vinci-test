using System;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes.Data
{
    [Serializable]
    public struct CutsceneFrame
    {
        public Sprite FrameSprite;

        [Tooltip("Время, сколько кадр висит на экране после появления до перехода к следующему (или закрытия)")]
        public float DurationSeconds;

        [Tooltip("Время плавного проявления (fade-in) в секундах")]
        public float FadeDuration;

        [Tooltip("Время анимации появления (pop-up, изменение масштаба) в секундах. 0 = без pop-up, кадр сразу в целевом размере")]
        public float PopDuration;
    }
}
