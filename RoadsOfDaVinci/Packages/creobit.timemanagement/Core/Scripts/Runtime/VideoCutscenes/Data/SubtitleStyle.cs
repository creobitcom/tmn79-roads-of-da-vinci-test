using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Data
{
    /// <summary>
    /// Вид субтитров: размер и цвет текста, плашка, положение на экране.
    /// Общий на все катсцены, лежит в CutsceneLibrarySO и настраивается
    /// в окне тайминга с живым превью. Сам шрифт (TMP asset) остаётся на префабе экрана.
    /// Размеры заданы в пикселях макета 1920×1080 — канвас масштабирует их сам.
    /// </summary>
    [Serializable]
    public class SubtitleStyle
    {
        [BoxGroup("Текст")]
        [LabelText("Шрифт")]
        [Tooltip("Пусто — остаётся шрифт, заданный на префабе экрана.")]
        public TMP_FontAsset Font;

        [BoxGroup("Текст")]
        [LabelText("Авто-размер")]
        public bool AutoSize = true;

        [BoxGroup("Текст")]
        [LabelText("Размер (мин)")]
        [ShowIf(nameof(AutoSize))]
        [MinValue(6f)]
        public float FontSizeMin = 24f;

        [BoxGroup("Текст")]
        [LabelText("Размер")]
        [MinValue(6f)]
        public float FontSizeMax = 44f;

        [BoxGroup("Текст")]
        [LabelText("Цвет текста")]
        public Color TextColor = Color.white;

        [BoxGroup("Плашка")]
        [LabelText("Цвет плашки")]
        public Color PlateColor = new(0f, 0f, 0f, 0.55f);

        [BoxGroup("Плашка")]
        [LabelText("Высота")]
        [MinValue(0f)]
        public float PlateHeight = 160f;

        [BoxGroup("Положение")]
        [LabelText("Отступ по бокам")]
        [MinValue(0f)]
        public float SideMargin = 120f;

        [BoxGroup("Положение")]
        [LabelText("Отступ снизу")]
        [MinValue(0f)]
        public float BottomOffset = 90f;
    }
}
