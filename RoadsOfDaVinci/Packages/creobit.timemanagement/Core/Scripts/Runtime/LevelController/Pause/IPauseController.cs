using R3;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause
{
    public interface IPauseController
    {
        public ReadOnlyReactiveProperty<bool> IsPaused { get; }
        public void Pause();
        public void Resume();
    }
}