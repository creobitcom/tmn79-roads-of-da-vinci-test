using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Creobit.EditionsUpgrade
{
    public class LevelPassTimerView : MonoBehaviour
    {
        public static LevelPassTimerView Instance { get; private set; }

        private static readonly FieldInfo LevelTimerPauseSourcesField =
            typeof(LevelTimer).GetField("_pauseSources", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo LevelTimerCurrentTimerField =
            typeof(LevelTimer).GetField("_currentTimer", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo LevelTimerParametersField =
            typeof(LevelTimer).GetField("_gameplayIntervalGeneralParameters", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly object LevelTimerGameModePauseSource =
            typeof(LevelTimer).GetField("GameModePauseSource", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null);

        private readonly CompositeDisposable _levelSubscription = new();

        private GameObject _displayRoot;
        private TMP_Text _timerText;

        private ILevelTimer _levelTimer;
        private ILevelController _levelController;

        private float _elapsedSeconds;
        private bool _isLevelStarted;
        private bool _isGloballyPaused;
        private bool _isEnabledByCheat;
        private bool _isTesterAvailable;
        private bool _isInGameplayScene;

        public bool IsEnabledByCheat => _isEnabledByCheat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
            {
                return;
            }

            var gameObject = new GameObject(nameof(LevelPassTimerView));
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<LevelPassTimerView>();
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            BuildDisplay();
            UpdateVisibility();

            SceneManager.sceneLoaded += SceneLoadedHandler;

            StartCoroutine(WaitForTesterAvailability());
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            SceneManager.sceneLoaded -= SceneLoadedHandler;
            UnsubscribeLevelFinished();
            _levelSubscription.Dispose();
        }

        private void Update()
        {
            if (!_isLevelStarted)
            {
                return;
            }

            var pauseSources = GetLevelTimerPauseSources();
            var isEasyPause = ContainsGameModeSource(pauseSources);

            if (!_isGloballyPaused && !ContainsTutorialSource(pauseSources))
            {
                _elapsedSeconds += Time.deltaTime;
            }

            if (!isEasyPause && TryGetLevelTimerSpentSeconds(out var spentSeconds))
            {
                SetTimerText(spentSeconds, false);
                return;
            }

            SetTimerText(_elapsedSeconds, isEasyPause);
        }

        public void SetEnabledByCheat(bool isEnabled)
        {
            _isEnabledByCheat = isEnabled;
            UpdateVisibility();
        }

        private IEnumerator WaitForTesterAvailability()
        {
            yield return new WaitUntil(() => TestersFeatures.Instance != null);
            yield return new WaitUntil(() => TestersFeatures.Instance.IsReady);

            _isTesterAvailable = TestersFeatures.Instance.IsAvailable;
            UpdateVisibility();
        }

        private void SceneLoadedHandler(Scene scene, LoadSceneMode mode)
        {
            _levelSubscription.Clear();
            UnsubscribeLevelFinished();

            _levelTimer = null;
            _isLevelStarted = false;
            _isGloballyPaused = false;
            _elapsedSeconds = 0f;
            SetTimerText(0f, false);

            _isInGameplayScene = scene.name.Equals("Gameplay", StringComparison.OrdinalIgnoreCase);
            UpdateVisibility();

            if (TryResolve<ILevelController>(out var levelController))
            {
                levelController.IsLevelStarted
                    .Subscribe(LevelStartedHandler)
                    .AddTo(_levelSubscription);

                _levelController = levelController;
                _levelController.LevelFinished += LevelFinishedHandler;
            }

            if (TryResolve<IPauseController>(out var pauseController))
            {
                pauseController.IsPaused
                    .Subscribe(isPaused => _isGloballyPaused = isPaused)
                    .AddTo(_levelSubscription);
            }

            TryResolve<ILevelTimer>(out _levelTimer);
        }

        private void LevelStartedHandler(bool isStarted)
        {
            _isLevelStarted = isStarted;

            if (isStarted)
            {
                _elapsedSeconds = 0f;
                SetTimerText(0f, false);
            }
        }

        private void LevelFinishedHandler(LevelBaseSO levelBase)
        {
            if (!_isTesterAvailable || !_isEnabledByCheat)
            {
                return;
            }

            var isEasyPause = ContainsGameModeSource(GetLevelTimerPauseSources());
            var seconds = !isEasyPause && TryGetLevelTimerSpentSeconds(out var spentSeconds)
                ? spentSeconds
                : _elapsedSeconds;
            var suffix = isEasyPause ? " EASY" : string.Empty;

            Debug.LogError(
                $"[LevelPassTimer] Level {levelBase.LevelNumber} passed in {FormatTime(seconds)} ({Mathf.RoundToInt(seconds)} sec){suffix}");
        }

        private void UnsubscribeLevelFinished()
        {
            if (_levelController == null)
            {
                return;
            }

            _levelController.LevelFinished -= LevelFinishedHandler;
            _levelController = null;
        }

        private HashSet<object> GetLevelTimerPauseSources()
        {
            if (_levelTimer == null || LevelTimerPauseSourcesField == null)
            {
                return null;
            }

            return LevelTimerPauseSourcesField.GetValue(_levelTimer) as HashSet<object>;
        }

        private static bool ContainsTutorialSource(HashSet<object> pauseSources)
        {
            if (pauseSources == null)
            {
                return false;
            }

            foreach (var source in pauseSources)
            {
                if (!ReferenceEquals(source, LevelTimerGameModePauseSource))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsGameModeSource(HashSet<object> pauseSources)
        {
            return pauseSources != null
                   && LevelTimerGameModePauseSource != null
                   && pauseSources.Contains(LevelTimerGameModePauseSource);
        }

        private bool TryGetLevelTimerSpentSeconds(out float spentSeconds)
        {
            spentSeconds = 0f;

            if (_levelTimer == null || LevelTimerCurrentTimerField == null || LevelTimerParametersField == null)
            {
                return false;
            }

            if (LevelTimerParametersField.GetValue(_levelTimer) is not GameplayIntervalGeneralParameters parameters
                || parameters.DurationSeconds <= 0f)
            {
                return false;
            }

            if (LevelTimerCurrentTimerField.GetValue(_levelTimer) is not float remainingSeconds)
            {
                return false;
            }

            spentSeconds = Mathf.Max(0f, parameters.DurationSeconds - remainingSeconds);
            return true;
        }

        private void UpdateVisibility()
        {
            _displayRoot.SetActive(_isTesterAvailable && _isEnabledByCheat && _isInGameplayScene);
        }

        private void SetTimerText(float seconds, bool isEasyMode)
        {
            var suffix = isEasyMode ? " EASY" : string.Empty;
            _timerText.text = $"{FormatTime(seconds)}{suffix}";
        }

        private static string FormatTime(float seconds)
        {
            var time = TimeSpan.FromSeconds(seconds);

            return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
        }

        private void BuildDisplay()
        {
            _displayRoot = new GameObject("Canvas");
            _displayRoot.transform.SetParent(transform, false);

            var canvas = _displayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            var scaler = _displayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var textGameObject = new GameObject("Text");
            textGameObject.transform.SetParent(_displayRoot.transform, false);

            _timerText = textGameObject.AddComponent<TextMeshProUGUI>();
            _timerText.fontSize = 36;
            _timerText.alignment = TextAlignmentOptions.TopRight;
            _timerText.color = Color.white;
            _timerText.textWrappingMode = TextWrappingModes.NoWrap;

            var rectTransform = _timerText.rectTransform;
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = new Vector2(-20f, -20f);
            rectTransform.sizeDelta = new Vector2(200f, 60f);

            SetTimerText(0f, false);
        }

        private static bool TryResolve<T>(out T result)
        {
            foreach (var scope in FindObjectsByType<LifetimeScope>(FindObjectsSortMode.None))
            {
                if (scope.Container != null && scope.Container.TryResolve(out result))
                {
                    return true;
                }
            }

            result = default;
            return false;
        }
    }
}
