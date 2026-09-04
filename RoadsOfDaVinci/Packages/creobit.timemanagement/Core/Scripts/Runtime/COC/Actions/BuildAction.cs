namespace _8floor.TimeManagement.Core.Scripts.Runtime.COC.Actions
{
    public class BuildAction : CocAction
    {
        public override CocActionType CocActionType => CocActionType.Build;
        
        public override void Action()
        {
            ComplexObjectView.BuildObject();
        }

        public override bool IsAvailable()
        {
            return ComplexObjectView.CanBuildObject();
        }
    }
}