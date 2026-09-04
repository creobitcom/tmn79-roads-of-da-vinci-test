using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Loading;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Systems
{
     public interface IGameplayIntervalsController : ILoadUnit, IReloadable, IDisposable
     {
          ushort StartInterval(GameplayIntervalSpecificParameters specificParameters,
               GameplayIntervalGeneralParameters generalParameters);

          void CancelInterval(ushort intervalId);
          
          void SetIntervalSpeed(ushort intervalId, float speed);

          void PauseInterval(ushort intervalId);

          void UnPauseInterval(ushort intervalId);
     }
}
