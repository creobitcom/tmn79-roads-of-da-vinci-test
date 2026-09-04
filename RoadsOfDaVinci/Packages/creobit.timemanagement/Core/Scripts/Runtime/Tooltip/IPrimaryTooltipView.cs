using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data;
using System.Collections.Generic;

public interface IPrimaryTooltipView : ITooltipView
{
    public void SetTooltipData(TooltipSettings tooltipSettings,
            IReadOnlyCollection<TooltipResourceView> inputResources,
            IReadOnlyCollection<TooltipResourceView> outputResources);
}
