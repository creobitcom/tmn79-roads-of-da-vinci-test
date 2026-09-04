namespace _8floor.TimeManagement.Core.Scripts.Runtime.COC.Actions
{
    public abstract class CocAction
    {
        protected ComplexObject ComplexObjectView;
        
        public abstract CocActionType CocActionType { get; }

        public void SetController(ComplexObject complexObjectView)
        {
            ComplexObjectView = complexObjectView;
        }

        public abstract void Action();

        public abstract bool IsAvailable();
    }

    public enum CocActionType
    {
        Interact,
        Build,
        Break
    }
}