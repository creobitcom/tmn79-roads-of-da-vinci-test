using _8floor.TimeManagement.Core.Scripts.Runtime.StandingPoint;
using Creobit.Loading;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.InactiveObjects
{
    public interface IInactiveAreaTransferController : ILoadUnit
    {
        void RegisterTransferable(InactiveObjectRefs inactiveArea);
        
        bool TryStartTransfer(StandingPointView targetPoint);
    }
}