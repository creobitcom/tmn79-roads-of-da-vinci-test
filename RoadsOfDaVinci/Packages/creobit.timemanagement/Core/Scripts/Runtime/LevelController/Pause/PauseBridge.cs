using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause
{
    public class PauseBridge : MonoBehaviour
    {
        private IPauseController _pauseController;

        [Inject]
        private void Construct(IPauseController pauseController)
        {
            _pauseController = pauseController;
        }

        public void Pause() => _pauseController.Pause();

        public void Resume() => _pauseController.Resume();
    }
}