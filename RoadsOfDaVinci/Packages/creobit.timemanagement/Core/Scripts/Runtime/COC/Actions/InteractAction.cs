namespace _8floor.TimeManagement.Core.Scripts.Runtime.COC.Actions
{
    public class InteractAction : CocAction
    {
        public override CocActionType CocActionType => CocActionType.Interact;
        
        public override void Action()
        {
            ComplexObjectView.InteractWithActiveObject();
        }

        public override bool IsAvailable()
        {
            return ComplexObjectView.CurrentObjectView.CanInteract;
        }
    }
}