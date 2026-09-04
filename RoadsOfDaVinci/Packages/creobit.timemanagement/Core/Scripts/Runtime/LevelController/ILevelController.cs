using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using Creobit.Loading;
using R3;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController
{
    public interface ILevelController : ILoadUnit, IDisposable
    {
        public ReadOnlyReactiveProperty<bool> IsLevelStarted { get; }
        public event Action FinishScreenShowed;
        public event Action WinViewShowing;
        public event Action<LevelBaseSO> LevelFinished;
        public event Action DialogueStarted;
        
        public void StartLevel();
        public void FinishLevel();
        public void NotifyWinViewShowing();
        public void SetPressAnythingToContinueObjectState(bool state);
        public void ShowFinishScreen();
        public void PrepareForNewLevel();
        public void ReloadLevel();
        public void Pause();
        public void Resume();
        public void DisableInput();
        public void EnableInput();
    }
}