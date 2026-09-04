using System;
using Creobit.Localization;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.Loading
{
    public class LoadingView : MonoBehaviour, IProgress<float>
    {
        [SerializeField] private Image _progressImage;
        [SerializeField] private TMP_Text _progressText;

        [Header("Smoothing")]
        [Tooltip("Скорость сближения бара с целью, 1/сек. Чем меньше, тем медленнее и плавнее. 0 - старое мгновенное поведение.")]
        [SerializeField, Min(0f)] private float _smoothing = 1.2f;
        [Tooltip("Потолок, выше которого бар не поднимается до фактического конца загрузки.")]
        [SerializeField, Range(0f, 1f)] private float _maxBeforeFinish = 0.9f;
        [Tooltip("Минимальное время показа заставки, сек.")]
        [SerializeField, Min(0f)] private float _minDuration = 1.5f;
        [Tooltip("Время добега до 100% перед закрытием, сек.")]
        [SerializeField, Min(0f)] private float _finishDuration = 0.4f;

        private string _progressTextKey;
        private float _target;
        private float _displayed;
        private float _activatedTime;
        private bool _isFinishing;

        private bool IsSmooth => _smoothing > 0f;

        public void Report(float value)
        {
            // прогресс монотонный: реальная доля скачет назад, когда загрузчик добавляет задачи по ходу
            _target = Mathf.Clamp01(Mathf.Max(_target, value));

            if (!IsSmooth)
            {
                SetFill(_target);
            }
        }

        public void Activate()
        {
            gameObject.SetActive(true);

            _target = 0f;
            _isFinishing = false;
            _activatedTime = Time.unscaledTime;
            SetFill(0f);

            if (string.IsNullOrEmpty(_progressTextKey))
            {
                _progressTextKey = _progressText.text;
            }

            _progressText.text = LocalizationService.Instance.GetText(_progressTextKey);

            LocalizationService.Instance.OnLanguageChanged -= LocalizationChangedHandler;
            LocalizationService.Instance.OnLanguageChanged += LocalizationChangedHandler;
        }

        /// <summary>
        /// Доводит бар до 100% и выдерживает минимальное время показа.
        /// </summary>
        public async UniTask Finish()
        {
            _target = 1f;

            if (!IsSmooth)
            {
                SetFill(1f);
                return;
            }

            _isFinishing = true;

            var from = _displayed;
            var leftToMinDuration = _minDuration - (Time.unscaledTime - _activatedTime);
            var duration = Mathf.Max(_finishDuration, leftToMinDuration);

            if (duration > 0f)
            {
                var elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    SetFill(Mathf.Lerp(from, 1f, Mathf.SmoothStep(0f, 1f, elapsed / duration)));

                    await UniTask.Yield();
                }
            }

            SetFill(1f);
        }

        private void Update()
        {
            if (!IsSmooth || _isFinishing)
            {
                return;
            }

            var goal = Mathf.Min(_target, _maxBeforeFinish);

            if (_displayed >= goal)
            {
                return;
            }

            // экспоненциальное сближение: не зависит от фреймрейта, бар всегда движется и замедляется у цели
            var step = 1f - Mathf.Exp(-_smoothing * Time.unscaledDeltaTime);

            SetFill(Mathf.Lerp(_displayed, goal, step));
        }

        private void SetFill(float value)
        {
            _displayed = value;
            _progressImage.fillAmount = value;
        }

        private void LocalizationChangedHandler(string language)
        {
            _progressText.text = LocalizationService.Instance.GetText(_progressTextKey);
        }

        public void Deactivate()
        {
            LocalizationService.Instance.OnLanguageChanged -= LocalizationChangedHandler;

            gameObject.SetActive(false);
        }
    }
}
