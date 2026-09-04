using System;
using System.Collections;
using Newtonsoft.Json;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    public class TestersFeatures : MonoBehaviour
    {
        private struct UserAttributes { }
        private struct AppAttributes { }

        public bool IsAvailable { get; private set; }

        public bool IsReady { get; private set; }

        public static TestersFeatures Instance { get; private set; }

        public event Action Ready;

        private const string COPY_DEVICE_ID_BOOL_NAME = "CopyDeviceID";

        private const string TESTERS_JSON_NAME = "Testers";

        private const float READY_TIMEOUT_SECONDS = 10f;

        private void OnDestroy()
        {
            RemoteConfigService.Instance.FetchCompleted -= OnConfigReceived;
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            IsAvailable = false;

            DontDestroyOnLoad(gameObject);

            RemoteConfigService.Instance.FetchCompleted += OnConfigReceived;

            StartCoroutine(SetReadyOnTimeout());

            InitializeUnityServices();
        }

        private IEnumerator SetReadyOnTimeout()
        {
            yield return new WaitForSecondsRealtime(READY_TIMEOUT_SECONDS);

            if (!IsReady)
            {
                Debug.LogWarning($"[TestersFeatures] Конфигурация тестеров не получена за {READY_TIMEOUT_SECONDS} с — доступ закрыт.");
            }

            SetReady();
        }

        private async void InitializeUnityServices()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TestersFeatures] Не удалось получить конфигурацию тестеров: {exception.Message}");
                SetReady();
            }
        }

        private void InitializeIsAvailable(Tester[] testers)
        {
            if (testers == null)
            {
                return;
            }

            string deviceID = SystemInfo.deviceUniqueIdentifier;

            for (int i = 0; i < testers.Length; i++)
            {
                if (testers[i].DeviceID == deviceID)
                {
                    IsAvailable = true;
                    return;
                }
            }
        }

        private void OnConfigReceived(ConfigResponse configResponse)
        {
            RuntimeConfig runtimeConfig = RemoteConfigService.Instance.appConfig;

            bool copyDeviceID = runtimeConfig.GetBool(COPY_DEVICE_ID_BOOL_NAME);
            string testersJson = runtimeConfig.GetJson(TESTERS_JSON_NAME, "");

            if (copyDeviceID)
            {
                GUIUtility.systemCopyBuffer = SystemInfo.deviceUniqueIdentifier;
            }

            if (testersJson != "")
            {
                InitializeIsAvailable(JsonConvert.DeserializeObject<Tester[]>(testersJson));
            }

            SetReady();
        }

        private void SetReady()
        {
            if (IsReady)
            {
                return;
            }

            IsReady = true;

            Ready?.Invoke();
        }
    }
}
