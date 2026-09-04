using Cysharp.Threading.Tasks;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager
{
    public interface IReactToActions
    {

        public ObjectViewInteractionType InteractionType { get; }
        public float InteractionTime { get; }

        public bool CanReactToPrimaryAction { get; }
        public bool AlwaysReactToPrimaryAction { get; set; }
        public bool CanReactToSecondaryAction { get; }
        public MovableObjectTaskView CurrentMovableObjectTaskView { get; set; }
        public float InteractionSpeed { get; set; }
        public bool IsInactiveBlocked { get; set; }

        public bool IsFree { get; } 

        /// <summary>
        /// Base primary action to gameplay objects. Subscribe to event from editor, call on primary action.
        /// </summary>
        public void OnPrimaryAction();
        public void OnSecondaryActionStart();
        public void OnSecondaryActionEnd();
        
        /// <summary>
        /// Base interact to gameplay objects. Calls when object task start (unit is here or task without units)
        /// </summary>
        public UniTask Interact(MovableObjectView unit = null);
    }
}