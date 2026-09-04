namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Cards
{
    /// <summary>
    /// Состояние одной ячейки тултипа. Определяет цвет числа и наличие красного круга.
    /// </summary>
    public enum TooltipCellState
    {
        /// <summary>Ресурса хватает — число зелёное, круга нет.</summary>
        Enough = 0,

        /// <summary>Ресурса не хватает — число красное, круг включён.</summary>
        NotEnough = 1,

        /// <summary>Нейтральная ячейка (выход крафта, таймер) — тёмное число, круга никогда нет.</summary>
        Neutral = 2,
    }
}
