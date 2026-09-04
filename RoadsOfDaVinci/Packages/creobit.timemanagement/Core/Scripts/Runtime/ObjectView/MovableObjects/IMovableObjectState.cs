using Cysharp.Threading.Tasks;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects
{
    public interface IMovableObjectState
    {
        public void StartState(MovableObjectView unit);

        public bool ChangeState(MovableObjectView unit, IMovableObjectState newState);

        public void EndState(MovableObjectView unit, bool async);
    }
}