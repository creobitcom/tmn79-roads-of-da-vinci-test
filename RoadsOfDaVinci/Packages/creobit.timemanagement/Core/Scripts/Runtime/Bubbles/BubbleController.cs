using System;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Creobit.Localization;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Bubbles
{
    public class BubbleController : IBubbleController
    {
        private readonly GameplaySceneReferences _sceneReferences;
        private readonly IPauseController _pauseController;
        private readonly IGameplayInputSystem _inputSystem;
        private readonly IObjectResolver _resolver;
        private readonly IReloadController _reloadController;
        private IDisposable _pauseSubscription; 
        
        private BubbleView _currentBubbleView;
        private Canvas _targetCanvas;

        private BubbleSequenceSO _currentSequence;
        private int _currentIndex;
        private Transform _currentAnchor;

        private CancellationTokenSource _timerCts;
        private CancellationTokenSource _moveCts;
        private bool _isPausingGameCurrently;

        private event Action _onSequenceCompleteCallback;

        [Inject]
        public BubbleController(
            GameplaySceneReferences sceneReferences,
            IPauseController pauseController,
            IGameplayInputSystem inputSystem,
            IObjectResolver resolver,
            IReloadController reloadController)
        {
            _sceneReferences = sceneReferences;
            _pauseController = pauseController;
            _inputSystem = inputSystem;
            _resolver = resolver;
            _reloadController = reloadController;
        }

        public UniTask Load()
        {
            _reloadController.AddReloadableObject(this);
            _targetCanvas = _sceneReferences.GameplayCanvasLayers[2];
            _inputSystem.OnMainButtonPressed += OnScreenClicked;
            _pauseSubscription = _pauseController.IsPaused.Skip(1).Subscribe(OnGlobalPauseChanged);
            
            return UniTask.CompletedTask;
        }

        private void OnGlobalPauseChanged(bool isPaused)
        {
            if (!isPaused && _isPausingGameCurrently)
            {
                _pauseController.Pause();
            }
        }
        
        public UniTask Reload()
        {
            CancelTimer();
            CancelMoveLoop();

            if (_isPausingGameCurrently)
            {
                _isPausingGameCurrently = false;
                _pauseController.Resume();
            }

            CloseSequenceView();
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            _pauseSubscription?.Dispose();
            _reloadController.RemoveReloadableObject(this);
            _inputSystem.OnMainButtonPressed -= OnScreenClicked;

            CancelTimer();
            CancelMoveLoop();

            if (_isPausingGameCurrently)
            {
                _isPausingGameCurrently = false;
                _pauseController.Resume();
            }

            if (_currentBubbleView != null)
            {
                Object.Destroy(_currentBubbleView.gameObject);
            }
        }

        public void Show(BubbleData bubbleData, Transform anchor)
        {
            var singleSequence = ScriptableObject.CreateInstance<BubbleSequenceSO>();
            singleSequence.InitializeForRuntime(new System.Collections.Generic.List<BubbleData> { bubbleData });
            
            ShowSequence(singleSequence, anchor);
        }

        public void Hide(BubbleData bubbleData)
        {
            if (_currentSequence != null && _currentIndex < _currentSequence.Bubbles.Count)
            {
                if (_currentSequence.Bubbles[_currentIndex] == bubbleData)
                {
                    HideCurrent();
                }
            }
        }

        public void ShowSequence(BubbleSequenceSO sequence, Transform anchor, Action onComplete = null)
        {
            if (sequence == null || sequence.Bubbles.Count == 0) return;

            _currentSequence = sequence;
            _currentIndex = 0;
            _currentAnchor = anchor;
            _onSequenceCompleteCallback = onComplete;

            if (_currentBubbleView == null)
            {
                _currentBubbleView = _resolver.Instantiate(_sceneReferences.BubblePrefab, _targetCanvas.transform);
                _currentBubbleView.gameObject.SetActive(false);
                _currentBubbleView.ButtonSkip.onClick.AddListener(SkipSequence);
            }

            ShowCurrentBubble();
        }

        public void HideCurrent()
        {
            if (_currentSequence == null || _currentIndex >= _currentSequence.Bubbles.Count) return;

            CancelTimer();
            CancelMoveLoop();

            if (_isPausingGameCurrently)
            {
                _isPausingGameCurrently = false;
                _pauseController.Resume();
            }

            _currentIndex++;
            if (_currentIndex < _currentSequence.Bubbles.Count)
            {
                ShowCurrentBubble();
            }
            else
            {
                _onSequenceCompleteCallback?.Invoke();
                CloseSequenceView();
            }
        }

        private void ShowCurrentBubble()
        {
            var data = _currentSequence.Bubbles[_currentIndex];

            string text = LocalizationService.Instance.GetText(data.TextLocalizationKey);
            if (string.IsNullOrEmpty(text)) text = data.TextLocalizationKey;

            _currentBubbleView.Setup(text, data.IsSkipButtonUsed, _currentAnchor != null);
            UpdateBubblePosition();
            _currentBubbleView.gameObject.SetActive(true);

            CancelMoveLoop();
            if (data.IsMovable)
            {
                _moveCts = new CancellationTokenSource();
                FollowAnchorLoop(_moveCts.Token).Forget();
            }

            if (data.IsPausingGame)
            {
                _isPausingGameCurrently = true;
                _inputSystem.IsActionAvailable = false; 
                _pauseController.Pause();
            }

            if (data.DisplayDuration > 0) StartTimer(data.DisplayDuration).Forget();
        }

        private void SkipSequence()
        {
            if (_currentSequence == null) return;

            CancelTimer();
            CancelMoveLoop();

            _onSequenceCompleteCallback?.Invoke();

            if (_isPausingGameCurrently)
            {
                _isPausingGameCurrently = false; 
                _inputSystem.IsActionAvailable = true; 
                _pauseController.Resume();
            }

            CloseSequenceView();
        }

        private void CloseSequenceView()
        {
            _currentSequence = null;
            _currentIndex = 0;
            _currentAnchor = null;
            _onSequenceCompleteCallback = null;

            if (_currentBubbleView != null)
            {
                _currentBubbleView.gameObject.SetActive(false);
            }
        }

        private void OnScreenClicked()
        {
            if (_currentSequence == null || _currentIndex >= _currentSequence.Bubbles.Count) return;

            var data = _currentSequence.Bubbles[_currentIndex];

            if (data.IsClosedByClick)
            {
                HideCurrent();
            }
        }

        private async UniTaskVoid StartTimer(float duration)
        {
            CancelTimer();
            _timerCts = new CancellationTokenSource();

            try
            {
                await UniTask.WaitForSeconds(duration, cancellationToken: _timerCts.Token);
                HideCurrent();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void CancelTimer()
        {
            if (_timerCts == null) return;
            _timerCts.Cancel();
            _timerCts.Dispose();
            _timerCts = null;
        }

        private async UniTaskVoid FollowAnchorLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, token);
                    UpdateBubblePosition();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void CancelMoveLoop()
        {
            if (_moveCts == null) return;
            _moveCts.Cancel();
            _moveCts.Dispose();
            _moveCts = null;
        }

        private void UpdateBubblePosition()
        {
            if (_currentSequence == null || _currentBubbleView == null) return;

            var data = _currentSequence.Bubbles[_currentIndex];
            var canvasPos = Vector2.zero;

            if (_currentAnchor == null)
            {
                if (_currentBubbleView.HasAnchor)
                {
                    SkipSequence();
                    return;
                }
            }
            else
            {
                canvasPos = UIHelper.ConvertWorldToLocalCanvasPosition(
                    _currentAnchor.position,
                    _sceneReferences.MainCamera,
                    _sceneReferences.MainCanvas,
                    Vector2.up, data.Offset,
                    _currentBubbleView.MainRect.rect.width,
                    _currentBubbleView.MainRect.rect.height);

                _currentBubbleView.SetPosition(canvasPos);

                var tailTargetCanvasPos = UIHelper.ConvertWorldToLocalCanvasPosition(
                    _currentAnchor.position,
                    _sceneReferences.MainCamera,
                    _sceneReferences.MainCanvas,
                    Vector2.zero, data.TailOffset, 0, 0);

                var localTailTarget = tailTargetCanvasPos - canvasPos;

                _currentBubbleView.SetTailTarget(localTailTarget);
                _currentBubbleView.TailPositionX = Mathf.Clamp(localTailTarget.x / 100f, -0.8f, 0.8f);
                return;
            }

            canvasPos = data.Offset;
            _currentBubbleView.SetPosition(canvasPos);
            _currentBubbleView.SetTailTarget(Vector2.zero);
        }
    }
}