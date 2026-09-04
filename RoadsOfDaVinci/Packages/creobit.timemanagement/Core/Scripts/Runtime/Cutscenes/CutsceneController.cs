using System;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes.UI;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Audio;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes
{
    public class CutsceneController : ICutsceneController, IDisposable
    {
        private readonly GameplaySceneReferences _sceneReferences;
        private readonly IPauseController _pauseController;
        private readonly IGameplayInputSystem _inputSystem;
        private readonly IReloadController _reloadController;
        private readonly IAudioService _audioService;
        private readonly IObjectResolver _resolver;

        private CancellationTokenSource _cts;

        // Клик: кольцо авто-закрытия пропадает, комикс закрывается только кнопкой.
        // Появление оставшихся фото при этом продолжается по DurationSeconds.
        private bool _closeTimerStopped;
        // Пока фото в зуме — пауза появления следующих; после ZoomOut — снова идёт.
        private bool _zoomPaused;
        private bool _skipRequested;
        private bool _closeRequested;

        // Пауза, поднятая самой катсценой: пока комикс открыт, таймер уровня и вся
        // продукция стоят. Флаг нужен, чтобы отличить свою паузу от паузы игрока.
        private bool _isPausingGameCurrently;
        private IDisposable _pauseSubscription;

        private ComicScene _activeScene;
        private CutscenePanelView _activePanel;

        private AudioClip _pendingSound;
        private float _pendingSoundDelay;

        public bool IsPlaying { get; private set; }

        public event Action CutsceneStarted;

        public CutsceneController(
            GameplaySceneReferences sceneReferences,
            IPauseController pauseController,
            IGameplayInputSystem inputSystem,
            IReloadController reloadController,
            IAudioService audioService,
            IObjectResolver resolver)
        {
            _sceneReferences = sceneReferences;
            _pauseController = pauseController;
            _inputSystem = inputSystem;
            _reloadController = reloadController;
            _audioService = audioService;
            _resolver = resolver;
        }

        public UniTask Load()
        {
            _reloadController.AddReloadableObject(this);

            _pauseSubscription = _pauseController.IsPaused.Skip(1).Subscribe(GlobalPauseChangedHandler);

            if (_sceneReferences.CutscenePrefab != null)
            {
                _sceneReferences.CutscenePrefab.gameObject.SetActive(false);
            }

            return UniTask.CompletedTask;
        }

        public UniTask Reload()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            _pendingSound = null;

            if (_sceneReferences.CutscenePrefab != null)
            {
                _sceneReferences.CutscenePrefab.Clear();
                _sceneReferences.CutscenePrefab.gameObject.SetActive(false);
            }

            if (IsPlaying)
            {
                _inputSystem.Enable();
                _inputSystem.IsActionAvailable = true;
                IsPlaying = false;
            }

            ResumeGameplay();

            return UniTask.CompletedTask;
        }

        public async UniTask PlayCutsceneAsync(CutsceneSequenceSO sequenceSO, Action onComplete = null)
        {
            if (IsPlaying) return;

            bool hasPrefab = sequenceSO != null && sequenceSO.ComicPrefab != null;
            bool hasFrames = sequenceSO != null && sequenceSO.Frames != null && sequenceSO.Frames.Count > 0;

            if (!hasPrefab && !hasFrames)
            {
                onComplete?.Invoke();
                return;
            }

            IsPlaying = true;
            CutsceneStarted?.Invoke();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            var cts = _cts;
            var token = cts.Token;

            _closeTimerStopped = false;
            _zoomPaused = false;
            _skipRequested = false;
            _closeRequested = false;
            _pendingSound = null;

            try
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);

                _inputSystem.Disable();
                _inputSystem.IsActionAvailable = false;

                PauseGameplay();

                if (hasPrefab)
                    await PlayPrefabAsync(sequenceSO, token);
                else
                    await PlayLegacyAsync(sequenceSO, token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                IsPlaying = false;
                _activeScene = null;
                _activePanel = null;

                ResumeGameplay();

                if (!cts.IsCancellationRequested)
                {
                    _inputSystem.Enable();
                    _inputSystem.IsActionAvailable = true;
                    onComplete?.Invoke();
                }
            }
        }

        private async UniTask PlayPrefabAsync(CutsceneSequenceSO sequenceSO, CancellationToken token)
        {
            var root = _sceneReferences.CutsceneRoot;
            if (root == null && _sceneReferences.CutscenePrefab != null)
                root = _sceneReferences.CutscenePrefab.transform.parent;

            if (root == null)
                Debug.LogWarning("[CutsceneController] CutsceneRoot не назначен и легаси-панель отсутствует — " +
                                 "комикс может отобразиться вне Canvas.");

            var instance = UnityEngine.Object.Instantiate(sequenceSO.ComicPrefab, root, false);
            _resolver.InjectGameObject(instance);

            var scene = instance.GetComponent<ComicScene>();

            if (scene == null)
            {
                Debug.LogError($"[CutsceneController] На префабе '{sequenceSO.ComicPrefab.name}' нет компонента ComicScene.");
                UnityEngine.Object.Destroy(instance);
                return;
            }

            if (scene.Photos.Count == 0)
                Debug.LogWarning($"[CutsceneController] У '{sequenceSO.ComicPrefab.name}' пустой список _photos — показывать нечего.");

            _activeScene = scene;
            scene.CloseRequested += HandleCloseRequested;
            scene.TimerStopRequested += HandleTimerStopRequested;
            scene.SkipPhotoRequested += HandleSkipPhotoRequested;
            scene.ZoomStateChanged += HandleZoomStateChanged;

            try
            {
                if (instance.transform is RectTransform rt)
                {
                    if (_sceneReferences.CutscenePrefab != null &&
                        _sceneReferences.CutscenePrefab.transform is RectTransform src)
                    {
                        rt.anchorMin = src.anchorMin;
                        rt.anchorMax = src.anchorMax;
                        rt.pivot = src.pivot;
                        rt.sizeDelta = src.sizeDelta;
                        rt.anchoredPosition = src.anchoredPosition;
                        rt.localScale = src.localScale;
                        rt.localRotation = src.localRotation;
                    }
                    else
                    {
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                        rt.localScale = Vector3.one;
                    }
                }

                instance.transform.SetAsLastSibling();
                instance.SetActive(true);

                var photos = scene.Photos;
                int count = photos.Count;

                scene.Prepare();

                float total = 0f;
                for (int i = 0; i < count; i++)
                    if (photos[i] != null)
                        total += Mathf.Max(0f, photos[i].DurationSeconds);

                int shown = 0;
                scene.ShowPhoto(0);
                ScheduleFrameSound(count > 0 ? photos[0] : null);
                scene.SetCloseProgress(total > 0f ? 0f : 1f);

                float elapsed = 0f;
                float next = count > 0 && photos[0] != null ? Mathf.Max(0f, photos[0].DurationSeconds) : 0f;

                while (!token.IsCancellationRequested && !_closeRequested)
                {
                    if (_skipRequested)
                    {
                        _skipRequested = false;

                        if (SkipOnePrefabPhoto(scene, photos, count, ref shown, ref next, ref elapsed))
                            PlayFrameSoundNow(photos[shown]);
                        else
                            FlushPendingSound();
                    }

                    if (!IsExternallyPaused && !_zoomPaused)
                        TickPendingSound(Time.deltaTime);

                    bool allPhotosStarted = count == 0 || shown + 1 >= count;

                    // Кольцо закрытия выключено и все фото уже стартовали — ждём кнопку.
                    if (_closeTimerStopped && allPhotosStarted)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        continue;
                    }

                    // Без клика — авто-закрытие по суммарному времени.
                    if (!_closeTimerStopped && elapsed >= total)
                        break;

                    if (IsExternallyPaused || _zoomPaused)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        continue;
                    }

                    elapsed += Time.deltaTime;

                    if (!_closeTimerStopped)
                        scene.SetCloseProgress(total > 0f ? elapsed / total : 1f);

                    while (shown + 1 < count && elapsed >= next)
                    {
                        shown++;
                        scene.ShowPhoto(shown);
                        ScheduleFrameSound(photos[shown]);
                        if (photos[shown] != null)
                            next += Mathf.Max(0f, photos[shown].DurationSeconds);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                if (!_closeTimerStopped)
                    scene.SetCloseProgress(1f);

                scene.Clear();
            }
            finally
            {
                scene.CloseRequested -= HandleCloseRequested;
                scene.TimerStopRequested -= HandleTimerStopRequested;
                scene.SkipPhotoRequested -= HandleSkipPhotoRequested;
                scene.ZoomStateChanged -= HandleZoomStateChanged;
                _activeScene = null;

                if (instance != null)
                    UnityEngine.Object.Destroy(instance);
            }
        }

        private static bool SkipOnePrefabPhoto(
            ComicScene scene,
            System.Collections.Generic.IReadOnlyList<ComicPhoto> photos,
            int count,
            ref int shown,
            ref float next,
            ref float elapsed)
        {
            if (count <= 0) return false;

            if (shown < count && scene.IsPhotoStarted(shown) && !scene.IsPhotoRevealed(shown))
            {
                scene.CompletePhotoShow(shown);
                return false;
            }

            if (shown + 1 >= count) return false;

            shown++;
            scene.CompletePhotoShow(shown);
            if (photos[shown] == null) return true;

            next += Mathf.Max(0f, photos[shown].DurationSeconds);
            elapsed = next;

            return true;
        }

        private void ScheduleFrameSound(ComicPhoto photo)
        {
            _pendingSound = null;

            if (photo == null || photo.Sound == null) return;

            float delay = Mathf.Max(0f, photo.SoundDelay);

            if (delay <= 0f)
            {
                _audioService.PlaySfx(photo.Sound);
                return;
            }

            _pendingSound = photo.Sound;
            _pendingSoundDelay = delay;
        }

        private void PlayFrameSoundNow(ComicPhoto photo)
        {
            _pendingSound = null;

            if (photo == null || photo.Sound == null) return;

            _audioService.PlaySfx(photo.Sound);
        }

        private void FlushPendingSound()
        {
            if (_pendingSound == null) return;

            var clip = _pendingSound;
            _pendingSound = null;
            _audioService.PlaySfx(clip);
        }

        private void TickPendingSound(float deltaTime)
        {
            if (_pendingSound == null) return;

            _pendingSoundDelay -= deltaTime;
            if (_pendingSoundDelay > 0f) return;

            var clip = _pendingSound;
            _pendingSound = null;
            _audioService.PlaySfx(clip);
        }

        private async UniTask PlayLegacyAsync(CutsceneSequenceSO sequenceSO, CancellationToken token)
        {
            var panelView = _sceneReferences.CutscenePrefab;
            if (panelView == null) return;

            _activePanel = panelView;
            panelView.gameObject.SetActive(true);
            panelView.transform.SetAsLastSibling();

            panelView.CloseRequested += HandleCloseRequested;
            panelView.TimerStopRequested += HandleTimerStopRequested;
            panelView.SkipPhotoRequested += HandleSkipPhotoRequested;
            panelView.ZoomStateChanged += HandleZoomStateChanged;

            try
            {
                var frames = sequenceSO.Frames;
                int count = Mathf.Min(frames.Count, 3);

                panelView.SetupLayout(frames.Count);

                float total = 0f;
                for (int i = 0; i < count; i++)
                    total += Mathf.Max(0f, frames[i].DurationSeconds);

                int shown = 0;
                panelView.ShowFrame(0, frames[0].FrameSprite, frames[0].FadeDuration, frames[0].PopDuration);
                panelView.SetCloseProgress(total > 0f ? 0f : 1f);

                float elapsed = 0f;
                float next = Mathf.Max(0f, frames[0].DurationSeconds);

                while (!token.IsCancellationRequested && !_closeRequested)
                {
                    if (_skipRequested)
                    {
                        _skipRequested = false;
                        SkipOneLegacyFrame(panelView, frames, count, ref shown, ref next, ref elapsed);
                    }

                    bool allPhotosStarted = count == 0 || shown + 1 >= count;

                    if (_closeTimerStopped && allPhotosStarted)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        continue;
                    }

                    if (!_closeTimerStopped && elapsed >= total)
                        break;

                    if (IsExternallyPaused || _zoomPaused)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        continue;
                    }

                    elapsed += Time.deltaTime;

                    if (!_closeTimerStopped)
                        panelView.SetCloseProgress(total > 0f ? elapsed / total : 1f);

                    while (shown + 1 < count && elapsed >= next)
                    {
                        shown++;
                        panelView.ShowFrame(shown, frames[shown].FrameSprite,
                            frames[shown].FadeDuration, frames[shown].PopDuration);
                        next += Mathf.Max(0f, frames[shown].DurationSeconds);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                if (!_closeTimerStopped)
                    panelView.SetCloseProgress(1f);

                panelView.Clear();
                panelView.gameObject.SetActive(false);
            }
            finally
            {
                panelView.CloseRequested -= HandleCloseRequested;
                panelView.TimerStopRequested -= HandleTimerStopRequested;
                panelView.SkipPhotoRequested -= HandleSkipPhotoRequested;
                panelView.ZoomStateChanged -= HandleZoomStateChanged;
                _activePanel = null;
            }
        }

        private static void SkipOneLegacyFrame(
            CutscenePanelView panelView,
            System.Collections.Generic.IReadOnlyList<CutsceneFrame> frames,
            int count,
            ref int shown,
            ref float next,
            ref float elapsed)
        {
            if (count <= 0) return;

            if (shown < count && panelView.IsPhotoStarted(shown) && !panelView.IsPhotoRevealed(shown))
            {
                panelView.CompleteFrameShow(shown);
                return;
            }

            if (shown + 1 >= count) return;

            shown++;
            panelView.ShowFrame(shown, frames[shown].FrameSprite, 0f, 0f);
            panelView.CompleteFrameShow(shown);

            next += Mathf.Max(0f, frames[shown].DurationSeconds);
            elapsed = next;
        }

        private void GlobalPauseChangedHandler(bool isPaused)
        {
            // Меню паузы, закрываясь, зовёт Resume и сняло бы нашу паузу вместе со своей.
            if (!isPaused && _isPausingGameCurrently)
            {
                _pauseController.Pause();
            }
        }

        private void PauseGameplay()
        {
            if (_isPausingGameCurrently) return;

            _isPausingGameCurrently = true;
            _pauseController.Pause();
        }

        private void ResumeGameplay()
        {
            if (!_isPausingGameCurrently) return;

            _isPausingGameCurrently = false;
            _pauseController.Resume();
        }

        /// <summary>Пауза, поднятая игроком, а не самой катсценой — только она замораживает показ кадров.</summary>
        private bool IsExternallyPaused => _pauseController.IsPaused.CurrentValue && !_isPausingGameCurrently;

        private void HandleCloseRequested() => _closeRequested = true;

        private void HandleTimerStopRequested()
        {
            if (_closeTimerStopped) return;

            _closeTimerStopped = true;
            _activeScene?.HideCloseProgress();
            _activePanel?.HideCloseProgress();
        }

        private void HandleSkipPhotoRequested() => _skipRequested = true;

        private void HandleZoomStateChanged(bool zoomed) => _zoomPaused = zoomed;

        public void Dispose()
        {
            _reloadController.RemoveReloadableObject(this);
            _cts?.Cancel();
            _cts?.Dispose();
            _pauseSubscription?.Dispose();

            if (IsPlaying)
            {
                _inputSystem.Enable();
                _inputSystem.IsActionAvailable = true;
            }

            ResumeGameplay();
        }
    }
}
