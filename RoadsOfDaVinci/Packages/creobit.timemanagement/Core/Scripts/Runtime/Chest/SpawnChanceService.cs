using UnityEngine;
using UltEvents;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Chest
{
    public class SpawnChanceService : MonoBehaviour
    {
        [SerializeField] private int _targetLevelToAlwaysSpawn = 12;
        [SerializeField] [Range(0, 100)] private int _chance = 50;

        public UltEvent OnSpawnAllowed;
        private ILevelLoader _levelLoader;

        [Inject]
        private void Construct(ILevelLoader levelLoader) => _levelLoader = levelLoader;

        public void TrySpawn()
        {
            int currentLvl = _levelLoader.LevelBaseSO.CurrentValue.LevelNumber;

            if (currentLvl == _targetLevelToAlwaysSpawn || Random.Range(0, 100) < _chance)
            {
                OnSpawnAllowed?.Invoke();
            }
        }
    }
}