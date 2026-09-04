using R3;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause
{
    public class PauseController : IPauseController
    {
        private readonly ReactiveProperty<bool> _isLevelPaused = new(false);

        public ReadOnlyReactiveProperty<bool> IsPaused => _isLevelPaused;
        
        public void Pause()
        {
            _isLevelPaused.Value = true;
        }

        public void Resume()
        {
            _isLevelPaused.Value = false;
        }
    }
}