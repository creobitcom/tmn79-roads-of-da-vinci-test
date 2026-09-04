using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.DoTweenAnimationExtensions;
using Creobit.Audio;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Altar
{
    public class AltarController : MonoBehaviour
    {
        [SerializeField] private List<StaticObjectView> _resourcesSequence;
        [SerializeField] private float _minDelay = 8f;
        [SerializeField] private float _maxDelay = 12f;
        [SerializeField] private AudioClip _altarProductionSound;

        private StaticObjectView _staticObjectView;

        private readonly Dictionary<StaticObjectView, DOTweenAnimation> _animationCache = new();

        private float _currentTimer;
        private float _targetTime;
        private int _currentIndex;

        private IAudioService _audioService;
        private IPauseController _pauseController;
        private bool _isPaused;
        private bool _isInitialized;

        [Inject]
        private void Construct(IPauseController pauseController, IAudioService audioService)
        {
            _pauseController = pauseController;
            _audioService = audioService;
            _isInitialized = true;
        }

        private void Start()
        {
            _staticObjectView = GetComponent<StaticObjectView>();

            if (_resourcesSequence == null || _resourcesSequence.Count == 0) return;

            foreach (var resource in _resourcesSequence)
            {
                if (resource == null) continue;

                resource.gameObject.SetActive(false);

                if (!_animationCache.ContainsKey(resource))
                {
                    var jumpComponent = resource.GetComponentInChildren<DoTweenJumpAnimation>(true);
                    if (jumpComponent != null && jumpComponent.TryGetComponent<DOTweenAnimation>(out var anim))
                    {
                        _animationCache.Add(resource, anim);
                    }
                }
            }

            StartProductionCycle().Forget();
        }

        private async UniTaskVoid StartProductionCycle()
        {
            await UniTask.WaitUntil(() => _isInitialized);

            _pauseController.IsPaused.Subscribe(x => _isPaused = x).AddTo(this);
            ResetTimer();

            while (this != null)
            {
                while (_currentTimer < _targetTime)
                {
                    if (!_isPaused && !_staticObjectView.productionData.BlockProduction)
                    {
                        _currentTimer += Time.deltaTime;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update);
                    if (this == null) return;
                }

                var currentResource = _resourcesSequence[_currentIndex];
                if (currentResource != null)
                {
                    currentResource.gameObject.SetActive(true);

                    if (_altarProductionSound != null && _audioService != null)
                    {
                        _audioService.PlaySfx(_altarProductionSound);
                    }

                    if (_animationCache.TryGetValue(currentResource, out var anim))
                    {
                        anim.RecreateTweenAndPlay();
                    }

                    await UniTask.WaitUntil(() => currentResource == null || !currentResource.gameObject.activeSelf,
                        cancellationToken: this.GetCancellationTokenOnDestroy());
                }

                _currentIndex = (_currentIndex + 1) % _resourcesSequence.Count;
                ResetTimer();
            }
        }

        private void ResetTimer()
        {
            _currentTimer = 0;
            _targetTime = Random.Range(_minDelay, _maxDelay);
        }
    }
}