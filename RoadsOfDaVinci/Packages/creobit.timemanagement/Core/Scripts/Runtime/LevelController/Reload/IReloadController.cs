using System;
using Cysharp.Threading.Tasks;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload
{
    public interface IReloadController
    {
        public event Action ReloadRequested;
        
        public void AddReloadableObject(IReloadable reloadableObject, int pos=-1);
        
        public void RemoveReloadableObject(IReloadable reloadableObject);

        public UniTask ReloadObjects();
        
        public void ReloadLevel();
    }
}