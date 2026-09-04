using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using TimeManagerEngine;
using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    public class ContinueGame : MonoBehaviour
    {
        public void Continue()
        {
#if TOYMAN
            // if (!DialogsManager.DialogActive)
            {
                AppTimer.Instance.gameTimeScale = 1.0f;
            }
#elif ARGUNOV
            GameManager.I.IsPause = false;
#elif CREOBIT
            var pauseBridge = FindFirstObjectByType<PauseBridge>();

            if (pauseBridge != null)
            {

                FindFirstObjectByType<PauseBridge>().Resume();
            }

#elif GAME_ON
            EmergencyCrewLevel.Instance.Paused = false;
#else
            Debug.LogError($"No handlers for continue", gameObject);
#endif
        }
    }
}