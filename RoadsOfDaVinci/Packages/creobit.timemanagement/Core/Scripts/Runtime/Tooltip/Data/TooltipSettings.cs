using System;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data
{
    [Serializable]
    public class TooltipSettings
    {
        [field: SerializeField]
        public TooltipData TooltipData { get; set; }

        [field: SerializeField]
        public bool ShowObjectImage { get; set; }
        
        [field: SerializeField]
        public bool ShowObjectName { get; private set; }
        
        [field: SerializeField]
        public bool ShowDescription { get; private set; }

        [field: SerializeField]
        public bool IsExchangeTooltip { get; private set; }

        /// <summary>
        /// Шаблон новой (LA8) карточки тултипа.
        /// Задан — объект показывается новой системой. Пусто — работает старая
        /// (обычный / обменный по IsExchangeTooltip). Так старые тултипы не ломаются.
        /// </summary>
        [field: SerializeField]
        public TooltipTemplate Template { get; private set; }

        /// <summary>
        /// Прятать тултип, когда на объекте больше нечего делать: все альтернативы исчерпаны
        /// либо кончились интеракции. Для раскопок — после того как всё выкопали, карточка
        /// оставалась висеть с одной шапкой и пустым телом.
        ///
        /// Работает только для ObjectView: у COC "доступность" определяется стадиями,
        /// и там этот вопрос решается сменой стадии, а не скрытием тултипа.
        /// По умолчанию выключено — поведение существующих объектов не меняется.
        /// </summary>
        [field: SerializeField]
        public bool HideWhenNothingToDo { get; private set; }

        [field: SerializeField]
        public Vector2 BaseTooltipOffset { get; private set; } = new(0, 150);
        
        [field: SerializeField]
        public Vector2 ResourcesTooltipOffset { get; private set; } = new(0, 150);
        
        [field: SerializeField]
        public Vector2 PathTooltipOffset { get; private set; } = new(0, 100);
    }
}