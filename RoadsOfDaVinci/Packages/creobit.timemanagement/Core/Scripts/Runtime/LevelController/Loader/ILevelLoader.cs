using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using R3;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader
{
    public interface ILevelLoader : IDisposable, ILoadUnit
    {
        public ReadOnlyReactiveProperty<LevelBaseSO> LevelBaseSO { get; }

        public event Action BeforeLevelLoaded;
        public event Action LevelLoaded;
        
        public UniTask LoadLevel(bool reload = false);
        public void LoadNextLevel();
    }
}