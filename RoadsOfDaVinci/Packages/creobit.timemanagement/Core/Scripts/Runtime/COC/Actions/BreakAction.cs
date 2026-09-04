namespace _8floor.TimeManagement.Core.Scripts.Runtime.COC.Actions
{
    public class BreakAction : CocAction
    {
        public override CocActionType CocActionType => CocActionType.Break;
        
        public override void Action()
        {
            ComplexObjectView.DestroyObject();
        }

        public override bool IsAvailable()
        {
            return ComplexObjectView.CanDestroyObject();
        }
    }
}