namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Cards
{
    /// <summary>
    /// Новая карточка тултипа (LA8). Сознательно НЕ наследует IPrimaryTooltipView:
    /// у того в контракте зашиты пуловые TooltipResourceView старой системы.
    /// Карточка сама владеет своими ячейками, поэтому ей нужен только общий ITooltipView
    /// (позиционирование) плюс приём данных.
    /// </summary>
    public interface ITooltipCardView : ITooltipView
    {
        /// <summary>
        /// Заполнить карточку. Любая секция данных может быть пустой — вьюха обязана
        /// спрятать соответствующий блок, а не упасть.
        /// </summary>
        void SetCardData(TooltipCardData data);
    }
}
