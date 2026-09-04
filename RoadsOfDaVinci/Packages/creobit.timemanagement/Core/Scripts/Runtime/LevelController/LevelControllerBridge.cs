using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController
{
    public class LevelControllerBridge : MonoBehaviour
    {
        private ILevelController _levelController;

        [Inject]
        private void Construct(ILevelController levelController)
        {
            _levelController = levelController;
        }

        public void ReloadLevel() => _levelController.ReloadLevel();
    }
}
