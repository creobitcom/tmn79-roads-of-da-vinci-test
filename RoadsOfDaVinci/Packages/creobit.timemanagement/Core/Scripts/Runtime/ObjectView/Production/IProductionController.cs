using System;
using Cysharp.Threading.Tasks;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.Production
{
    public interface IProductionController : IDisposable
    {
        public void ChangeSpawnPoint();
        
        public UniTaskVoid StartProduction();

        public void StopProduction();

        public void SetSpeed(float speed);
        
        public void DisableProduction();
    }
}