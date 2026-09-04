using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer.Data
{
    [Serializable]
    public class LevelStarData
    {
        [field: SerializeField]
        [field: AssetsOnly]
        public Image StarPrefab { get; private set; }

        [field: SerializeField]
        [field: LabelText("Пройти за, сек")]
        [field: Tooltip("Лимит ПОТРАЧЕННОГО времени с начала уровня, а не остаток таймера. " +
                        "Уложился в это значение — звезда засчитана.")]
        public ushort TimeLeftMoreThan { get; private set; }

        [ShowInInspector]
        [DisplayAsString]
        [LabelText("мм:сс")]
        [TableColumnWidth(70, false)]
        private string TimeLimitFormatted => $"{TimeLeftMoreThan / 60:00}:{TimeLeftMoreThan % 60:00}";
    }
}