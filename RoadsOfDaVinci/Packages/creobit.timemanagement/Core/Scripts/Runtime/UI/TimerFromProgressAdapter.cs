using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.UI
{
    public class TimerFromProgressAdapter : MonoBehaviour
    {
        [SerializeField] private Image _progressBar;
        [SerializeField] private TextMeshProUGUI _timerText;
        [SerializeField] private GameObject _backgroundContainer;

        private float _accumulatedTime;
        private float _lastProgress;
        private float _estimatedTotalDuration;
        private int _lastDisplayedSeconds = -1;

        private void OnEnable()
        {
            ResetTimer();
        }

        private void OnDisable()
        {
            if (_backgroundContainer) _backgroundContainer.SetActive(false);
        }

        private void ResetTimer()
        {
            _accumulatedTime = 0f;
            _lastProgress = 0f;
            _estimatedTotalDuration = 0f;
            _lastDisplayedSeconds = -1;
            
            if (_backgroundContainer) 
                _backgroundContainer.SetActive(false);
        }

        private void Update()
        {
            if (_progressBar == null || _timerText == null) return;

            float currentProgress = _progressBar.fillAmount;
            
            if (currentProgress <= 0.001f || currentProgress >= 1f)
            {
                if (_backgroundContainer.activeSelf) 
                    ResetTimer();
                return;
            }

            if (!_backgroundContainer.activeSelf)
            {
                _backgroundContainer.SetActive(true);
            }

            float progressDelta = currentProgress - _lastProgress;

            if (progressDelta < 0)
            {
                ResetTimer();
                return;
            }
            
            if (progressDelta > 0)
            {
                _accumulatedTime += Time.deltaTime;
            }

            _lastProgress = currentProgress;

            float remainingSeconds = 0f;
            
            if (currentProgress > 0.02f) 
            {
                if (_estimatedTotalDuration == 0f || currentProgress < 0.1f)
                {
                    _estimatedTotalDuration = _accumulatedTime / currentProgress;
                }

                remainingSeconds = Mathf.Max(0, _estimatedTotalDuration - _accumulatedTime);
            }
            
            int secondsToInt = Mathf.FloorToInt(remainingSeconds + 0.99f);
            
            if (secondsToInt != _lastDisplayedSeconds)
            {
                _lastDisplayedSeconds = secondsToInt;
                TimeSpan t = TimeSpan.FromSeconds(secondsToInt);
                _timerText.text = string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
            }
        }
    }
}