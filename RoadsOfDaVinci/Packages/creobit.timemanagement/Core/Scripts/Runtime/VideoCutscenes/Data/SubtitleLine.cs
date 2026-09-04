using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Data
{
    /// <summary>
    /// Одна строка субтитра: интервал показа по времени ролика и ключ локализации.
    /// Сам текст лежит в GameText_*.json, как весь остальной текст игры.
    /// </summary>
    [Serializable]
    public class SubtitleLine
    {
        [HorizontalGroup("time", Width = 90f)]
        [LabelText("с")]
        [LabelWidth(14f)]
        [Tooltip("Секунда ролика, с которой строка появляется.")]
        public float Start;

        [HorizontalGroup("time", Width = 90f)]
        [LabelText("по")]
        [LabelWidth(20f)]
        [Tooltip("Секунда ролика, на которой строка пропадает.")]
        public float End;

        [HorizontalGroup("time")]
        [LabelText("ключ")]
        [LabelWidth(34f)]
        [Tooltip("Ключ локализации из GameText_*.json.")]
        public string LocKey;

        public float Duration => Mathf.Max(0f, End - Start);

        public bool Contains(float time) => time >= Start && time < End;
    }
}
