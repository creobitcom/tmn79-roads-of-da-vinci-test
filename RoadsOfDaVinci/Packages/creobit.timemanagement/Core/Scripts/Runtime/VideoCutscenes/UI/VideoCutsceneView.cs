using System;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Data;
using Creobit.Localization;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.UI
{
    /// <summary>
    /// Экран видео-катсцены: полноэкранный ролик, субтитры и кнопка пропуска.
    /// Спавнится контроллером на время ролика и уничтожается после.
    /// Субтитры тикают от VideoPlayer.time, поэтому не разъезжаются при подгрузке.
    /// </summary>
    public class VideoCutsceneView : MonoBehaviour
    {
        [Header("Видео")]
        [SerializeField] private VideoPlayer _videoPlayer;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private RawImage _screen;
        [SerializeField] private AspectRatioFitter _aspectFitter;

        [Header("Субтитры")]
        [SerializeField] private TMP_Text _subtitleText;
        [SerializeField] private GameObject _subtitlePlate;

        [Header("Пропуск")]
        [SerializeField] private Button _skipButton;

        [Header("Появление")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeDuration = 0.15f;

        [Header("Порядок отрисовки")]
        [Tooltip("Чем больше, тем выше поверх интерфейса меты. Панель читов сидит на 32767 и остаётся сверху намеренно.")]
        [SerializeField] private int _sortingOrder = 1000;

        [Header("Защита от зависания")]
        [Tooltip("Сколько ждать готовности ролика, прежде чем сдаться и пропустить катсцену.")]
        [SerializeField] private float _prepareTimeout = 15f;

        private VideoCutsceneSO _data;
        private RenderTexture _renderTexture;
        private string[] _resolvedLines;
        private int _currentLine = -1;
        private bool _finished;
        private bool _failed;

        /// <summary>Кадр готов и ролик пошёл — можно убирать затемнение перехода.</summary>
        public event Action Started;

        private void Awake()
        {
            BringToFront();

            SetSubtitle(null);

            if (_skipButton != null)
            {
                _skipButton.gameObject.SetActive(false);
                _skipButton.onClick.AddListener(Skip);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
        }

        private void OnDestroy()
        {
            if (_skipButton != null)
            {
                _skipButton.onClick.RemoveListener(Skip);
            }

            if (_videoPlayer != null)
            {
                _videoPlayer.loopPointReached -= LoopPointReachedHandler;
                _videoPlayer.errorReceived -= ErrorReceivedHandler;
                _videoPlayer.Stop();
                _videoPlayer.targetTexture = null;
            }

            ReleaseRenderTexture();
        }

        /// <summary>
        /// Поднимает экран поверх интерфейса меты. Делается в рантайме, а не в префабе:
        /// Unity сбрасывает overrideSorting у канваса, который в момент сохранения префаба
        /// был корневым, поэтому сохранённое в ассете значение доверять нельзя.
        /// </summary>
        private void BringToFront()
        {
            transform.SetAsLastSibling();

            var canvas = GetComponent<Canvas>();

            if (canvas == null)
            {
                return;
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = _sortingOrder;
        }

        /// <summary>
        /// Применяет общий стиль субтитров. Размеры заданы в пикселях макета,
        /// канвас доводит их до реального разрешения сам.
        /// </summary>
        public void ApplyStyle(SubtitleStyle style)
        {
            if (style == null)
            {
                return;
            }

            if (_subtitlePlate != null)
            {
                if (_subtitlePlate.TryGetComponent<Image>(out var plateImage))
                {
                    plateImage.color = style.PlateColor;
                }

                var rect = (RectTransform)_subtitlePlate.transform;

                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.offsetMin = new Vector2(style.SideMargin, style.BottomOffset);
                rect.offsetMax = new Vector2(-style.SideMargin, style.BottomOffset + style.PlateHeight);
            }

            if (_subtitleText == null)
            {
                return;
            }

            if (style.Font != null)
            {
                _subtitleText.font = style.Font;
            }

            _subtitleText.color = style.TextColor;
            _subtitleText.enableAutoSizing = style.AutoSize;

            if (style.AutoSize)
            {
                _subtitleText.fontSizeMin = style.FontSizeMin;
                _subtitleText.fontSizeMax = style.FontSizeMax;
            }
            else
            {
                _subtitleText.fontSize = style.FontSizeMax;
            }
        }

        /// <summary>
        /// Пускает звук ролика через микшер игры. Без этого AudioSource играет напрямую
        /// и игнорирует настройки громкости и отключение звука.
        /// </summary>
        public void ApplyAudioGroup(AudioMixerGroup group)
        {
            if (group == null || _audioSource == null)
            {
                return;
            }

            _audioSource.outputAudioMixerGroup = group;
        }

        public async UniTask PlayAsync(VideoCutsceneSO data)
        {
            _data = data;

            if (_data == null || _videoPlayer == null)
            {
                return;
            }

            var token = this.GetCancellationTokenOnDestroy();

            ResolveSubtitles();

            if (!await PrepareAsync(token))
            {
                return;
            }

            SetupScreen();

            await FadeAsync(1f, token);

            ShowSkipButtonAsync(token).Forget();

            _videoPlayer.Play();

            Started?.Invoke();

            var cancelled = await UniTask.WaitUntil(() => _finished || _failed, cancellationToken: token)
                .SuppressCancellationThrow();

            if (cancelled)
            {
                return;
            }

            SetSubtitle(null);

            await FadeAsync(0f, token);
        }

        private async UniTask<bool> PrepareAsync(CancellationToken token)
        {
            var url = _data.GetVideoUrl();

            if (string.IsNullOrEmpty(url))
            {
                Log.Meta.Error($"VideoCutscene {_data.Id}: не указан файл ролика.");
                return false;
            }

            _videoPlayer.source = VideoSource.Url;
            _videoPlayer.url = url;
            _videoPlayer.isLooping = false;
            _videoPlayer.playOnAwake = false;
            _videoPlayer.waitForFirstFrame = true;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.skipOnDrop = true;

            if (_audioSource != null)
            {
                _videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                _videoPlayer.EnableAudioTrack(0, true);
                _videoPlayer.SetTargetAudioSource(0, _audioSource);
                _audioSource.volume = _data.Volume;
            }

            _videoPlayer.loopPointReached += LoopPointReachedHandler;
            _videoPlayer.errorReceived += ErrorReceivedHandler;

            _videoPlayer.Prepare();

            var deadline = Time.realtimeSinceStartup + _prepareTimeout;

            var cancelled = await UniTask
                .WaitUntil(() => _videoPlayer.isPrepared || _failed || Time.realtimeSinceStartup > deadline,
                    cancellationToken: token)
                .SuppressCancellationThrow();

            if (cancelled || _failed)
            {
                return false;
            }

            if (!_videoPlayer.isPrepared)
            {
                Log.Meta.Error($"VideoCutscene {_data.Id}: ролик не подготовился за {_prepareTimeout} сек ({url}).");
                return false;
            }

            return true;
        }

        private void SetupScreen()
        {
            var width = (int)_videoPlayer.width;
            var height = (int)_videoPlayer.height;

            if (width <= 0 || height <= 0)
            {
                width = 1920;
                height = 1080;
            }

            _renderTexture = new RenderTexture(width, height, 0)
            {
                name = $"RT_{_data.Id}"
            };

            _videoPlayer.targetTexture = _renderTexture;

            if (_screen != null)
            {
                _screen.texture = _renderTexture;
                _screen.color = Color.white;
            }

            if (_aspectFitter != null)
            {
                _aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                _aspectFitter.aspectRatio = (float)width / height;
            }
        }

        private void ResolveSubtitles()
        {
            var lines = _data.Subtitles;

            _resolvedLines = new string[lines.Count];

            for (int i = 0; i < lines.Count; i++)
            {
                var key = lines[i]?.LocKey;

                _resolvedLines[i] = string.IsNullOrEmpty(key)
                    ? string.Empty
                    : LocalizationService.Instance.GetText(key);
            }
        }

        private void Update()
        {
            if (_data == null || _videoPlayer == null)
            {
                return;
            }

            UpdateSubtitles();

            if (_data.CanSkip
                && _skipButton != null
                && _skipButton.gameObject.activeSelf
                && Keyboard.current != null
                && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Skip();
            }
        }

        private void UpdateSubtitles()
        {
            if (_resolvedLines == null || _resolvedLines.Length == 0 || !_videoPlayer.isPlaying)
            {
                return;
            }

            var time = (float)_videoPlayer.time;
            var lines = _data.Subtitles;

            // Почти каждый кадр активна та же строка, что и в прошлом — проверяем её первой.
            if (_currentLine >= 0 && _currentLine < lines.Count
                                  && lines[_currentLine] != null && lines[_currentLine].Contains(time))
            {
                return;
            }

            var index = -1;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] != null && lines[i].Contains(time))
                {
                    index = i;
                    break;
                }
            }

            if (index == _currentLine)
            {
                return;
            }

            _currentLine = index;

            SetSubtitle(index < 0 ? null : _resolvedLines[index]);
        }

        private void SetSubtitle(string text)
        {
            var visible = !string.IsNullOrEmpty(text);

            if (_subtitleText != null)
            {
                _subtitleText.text = visible ? text : string.Empty;
            }

            if (_subtitlePlate != null && _subtitlePlate.activeSelf != visible)
            {
                _subtitlePlate.SetActive(visible);
            }
        }

        private async UniTaskVoid ShowSkipButtonAsync(CancellationToken token)
        {
            if (_skipButton == null || !_data.CanSkip)
            {
                return;
            }

            if (_data.SkipButtonDelay > 0f)
            {
                var cancelled = await UniTask
                    .Delay(TimeSpan.FromSeconds(_data.SkipButtonDelay), cancellationToken: token)
                    .SuppressCancellationThrow();

                if (cancelled || _finished || _failed)
                {
                    return;
                }
            }

            _skipButton.gameObject.SetActive(true);
        }

        private void Skip()
        {
            if (_finished)
            {
                return;
            }

            Log.Meta.Info($"VideoCutscene {_data?.Id}: пропущено игроком.");

            _finished = true;

            if (_videoPlayer != null)
            {
                _videoPlayer.Stop();
            }
        }

        private async UniTask FadeAsync(float target, CancellationToken token)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            if (_fadeDuration <= 0f)
            {
                _canvasGroup.alpha = target;
                return;
            }

            var start = _canvasGroup.alpha;
            var elapsed = 0f;

            while (elapsed < _fadeDuration)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / _fadeDuration);

                await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            }

            _canvasGroup.alpha = target;
        }

        private void LoopPointReachedHandler(VideoPlayer source) => _finished = true;

        private void ErrorReceivedHandler(VideoPlayer source, string message)
        {
            Log.Meta.Error($"VideoCutscene {_data?.Id}: ошибка проигрывания — {message}");
            _failed = true;
        }

        private void ReleaseRenderTexture()
        {
            if (_renderTexture == null)
            {
                return;
            }

            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }
    }
}
