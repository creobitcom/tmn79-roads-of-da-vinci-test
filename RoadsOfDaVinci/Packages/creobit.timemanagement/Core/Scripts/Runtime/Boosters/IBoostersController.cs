using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using Creobit.Loading;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Boosters 
{

    public interface IBoostersController : IDisposable, IPausable, ILoadUnit
    {
        public List<BoosterView> Boosters { get; }
        
        public UniTask UseBooster(BoosterView boosterView);

        public UniTask ChargeBooster(BoosterView boosterView);

        public void Reload();

        public event Action<BoosterDataSO> OnBoosterUsedEvent; 
    }
}
