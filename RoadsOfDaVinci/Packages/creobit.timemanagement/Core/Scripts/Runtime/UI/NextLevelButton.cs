using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.UI
{
    [RequireComponent(typeof(Button))]
    public class NextLevelButton : MonoBehaviour
    {
        private ILevelLoader _levelLoader;
        private ILevelController _levelController;

        [Inject]
        private void Construct(ILevelLoader levelLoader,
            ILevelController levelController)
        {
            _levelLoader = levelLoader;
            _levelController = levelController;
        }

        public void LoadNextLevel()
        { 
            _levelLoader.LoadNextLevel();
        }

        public void ReloadLevel()
        {
            _levelController.ReloadLevel();
        }

        public void LoadMap()
        {
            SceneManager.LoadScene(0);
        }
    }
}
