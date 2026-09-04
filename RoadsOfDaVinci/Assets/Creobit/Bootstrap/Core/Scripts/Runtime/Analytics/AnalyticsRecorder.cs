#if UNITY_ANALYTICS
using Unity.Services.Analytics;
using Unity.Services.Core;
#endif
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Analytics
{
    public class AnalyticsRecorder
    {
        public const string ConsentPrefKey = "AnalyticsIsEnabled";

        public static AnalyticsRecorder Instance { get; private set; }

        public AnalyticsRecorder()
        {
            Instance = this;
        }

        public async UniTask StartAfterConsent()
        {
#if UNITY_ANALYTICS
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (PlayerPrefs.GetInt(ConsentPrefKey, 1) > 0)
                {
                    AnalyticsService.Instance.StartDataCollection();
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Analytics initialization failed: {exception.Message}");
            }
#else
            await UniTask.CompletedTask;
#endif
        }

        public void LevelStarted(int levelNumber)
        {
#if UNITY_ANALYTICS
            var levelStartedEvent = new CustomEvent("LevelStarted");
            levelStartedEvent.Add("levelNumber", levelNumber);
            Record(levelStartedEvent);
#endif
        }

        public void LevelRestarted(int levelNumber)
        {
#if UNITY_ANALYTICS
            var levelRestartedEvent = new CustomEvent("LevelRestarted");
            levelRestartedEvent.Add("levelNumber", levelNumber);
            Record(levelRestartedEvent);
#endif
        }

        public void LevelCompleted(int levelNumber, int stars)
        {
#if UNITY_ANALYTICS
            var levelCompletedEvent = new CustomEvent("LevelCompleted");
            levelCompletedEvent.Add("levelNumber", levelNumber);
            levelCompletedEvent.Add("stars", stars);
            Record(levelCompletedEvent);
#endif
        }

        public void InappPurchased(string place, int levelNumber)
        {
#if UNITY_ANALYTICS
            var inappPurchasedEvent = new CustomEvent("InappPurchased");
            inappPurchasedEvent.Add("place", place);
            inappPurchasedEvent.Add("levelNumber", levelNumber);
            Record(inappPurchasedEvent);
#endif
        }

        public void MapOpened()
        {
#if UNITY_ANALYTICS
            Record("ScreenMapOpened");
#endif
        }

        public void TrophyPanelOpened()
        {
#if UNITY_ANALYTICS
            Record("ScreenTrophiesOpened");
#endif
        }

        public void SettingsPanelOpened()
        {
#if UNITY_ANALYTICS
            Record("ScreenSettingsOpened");
#endif
        }

        public void AllTrophiesWon()
        {
#if UNITY_ANALYTICS
            Record("AllTrophiesWon");
#endif
        }

#if UNITY_ANALYTICS
        private static void Record(CustomEvent customEvent)
        {
            AnalyticsService.Instance.RecordEvent(customEvent);
            AnalyticsService.Instance.Flush();
        }

        private static void Record(string eventName)
        {
            AnalyticsService.Instance.RecordEvent(eventName);
            AnalyticsService.Instance.Flush();
        }
#endif
    }
}
