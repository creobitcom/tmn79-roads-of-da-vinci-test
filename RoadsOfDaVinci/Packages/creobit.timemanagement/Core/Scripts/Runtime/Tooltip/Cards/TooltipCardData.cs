using System.Collections.Generic;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Cards
{
    /// <summary>
    /// Данные одной ячейки: иконка + подпись + состояние.
    /// Подпись — строка, а не int, чтобы одной ячейкой рисовать и ресурс ("88"), и таймер ("1:30").
    /// </summary>
    public readonly struct TooltipCellData
    {
        public readonly Sprite Icon;
        public readonly string Text;
        public readonly TooltipCellState State;

        public TooltipCellData(Sprite icon, string text, TooltipCellState state)
        {
            Icon = icon;
            Text = text;
            State = state;
        }
    }

    /// <summary>
    /// Группа ячеек. Обычный тултип = одна группа входа; альтернативы = несколько групп,
    /// между которыми карточка сама рисует разделитель "oder".
    /// </summary>
    public sealed class TooltipGroupData
    {
        public readonly List<TooltipCellData> Cells = new();

        /// <summary>Хватает ли игроку на ВСЮ эту группу целиком.</summary>
        public bool IsAffordable;
    }

    /// <summary>
    /// Блок уровня фабрики: время производства + производимый ресурс.
    /// </summary>
    public sealed class TooltipStageData
    {
        /// <summary>Номер уровня для показа игроку (1-based).</summary>
        public int DisplayLevel;

        /// <summary>Ячейка таймера. Icon = иконка часов из префаба, Text = отформатированное время.</summary>
        public TooltipCellData Timer;

        /// <summary>Ячейки производимых ресурсов.</summary>
        public readonly List<TooltipCellData> Output = new();
    }

    /// <summary>
    /// Полезная нагрузка карточки тултипа. Все секции опциональны — вьюха прячет то, чего нет,
    /// и не должна падать ни на одном пустом поле (требование "частичное заполнение данных").
    /// </summary>
    public sealed class TooltipCardData
    {
        /// <summary>Ключ локализации имени объекта. Пусто — шапка прячется.</summary>
        public string NameKey;

        public string NameSuffixKey;

        /// <summary>
        /// Картинка-превью в рамке сбоку: что игрок получит после действия.
        /// Задаётся на объекте уровня (StaticObjectView), потому что на каждом уровне
        /// закопано своё. null — блок рамки прячется.
        /// </summary>
        public Sprite PreviewIcon;

        /// <summary>Агрегат: хватает ли на действие. Красит шапку в зелёный/красный.</summary>
        public bool IsAffordable;

        /// <summary>Группы входа. Пусто — блок входа и стрелка прячутся.</summary>
        public readonly List<TooltipGroupData> InputGroups = new();

        /// <summary>Получаемые ресурсы. Пусто — блок выхода и стрелка прячутся.</summary>
        public readonly List<TooltipCellData> Output = new();

        /// <summary>Текущий уровень фабрики. null — блок прячется.</summary>
        public TooltipStageData CurrentStage;

        /// <summary>Следующий уровень фабрики. null (максимальный уровень) — блок прячется.</summary>
        public TooltipStageData NextStage;
    }
}
