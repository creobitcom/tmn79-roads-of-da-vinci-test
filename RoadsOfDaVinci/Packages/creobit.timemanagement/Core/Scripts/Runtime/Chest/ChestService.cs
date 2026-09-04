using UnityEngine;
using UltEvents;
using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using Creobit.Audio;
using Cysharp.Threading.Tasks;
using VContainer;
using R3;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Chest
{
    public class ChestService : MonoBehaviour, IPausable
    {
        [Header("Settings")] [SerializeField] private int _clicksToOpen = 3;
        [SerializeField] private Vector2Int _rewardRange = new Vector2Int(3, 7);

        [Header("Blink Settings")] [SerializeField]
        private string _blinkBoolName = "Blink";

        [SerializeField] private float _blinkDuration = 0.15f;

        [Header("Resources to Give")] [SerializeField]
        private ResourceBaseSO[] _chestRewards;

        [Header("Audio Configuration")] [SerializeField]
        private AudioSource _ambientAudioSource;

        [SerializeField] private AudioClip _clickSound;
        [SerializeField] private AudioClip _deathSound;

        [Header("Events")] public UltEvent OnClickReceived;
        public UltEvent OnChestOpened;

        private int _currentClicks = 0;
        private bool _isDead = false;
        private bool _isPaused = false;
        private Animator _animator;

        private IGameResourcesSystem _resourcesSystem;
        private GameplaySceneReferences _sceneRefs;
        private IAudioService _audioService;
        private IPauseController _pauseController;

        private readonly CompositeDisposable _disposable = new();

        [Inject]
        private void Construct(
            IGameResourcesSystem resourcesSystem,
            GameplaySceneReferences sceneRefs,
            IAudioService audioService,
            IPauseController pauseController)
        {
            _resourcesSystem = resourcesSystem;
            _sceneRefs = sceneRefs;
            _audioService = audioService;
            _pauseController = pauseController;
        }

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();

            _pauseController.IsPaused
                .Skip(1)
                .Subscribe(OnPausedStateChanged)
                .AddTo(_disposable);
        }

        private void OnEnable()
        {
            _isDead = false;
            _currentClicks = 0;
            _isPaused = _pauseController != null && _pauseController.IsPaused.CurrentValue;
        }

        private void OnDisable() => StopAmbient();

        private void OnDestroy() => _disposable.Dispose();

        private void OnPausedStateChanged(bool isPaused)
        {
            if (isPaused)
            {
                Pause();
            }
            else
            {
                Unpause();
            }
        }

        public void Pause()
        {
            _isPaused = true;
            if (_ambientAudioSource != null && _ambientAudioSource.isPlaying)
            {
                _ambientAudioSource.Pause();
            }
        }

        public void Unpause()
        {
            _isPaused = false;
            if (_ambientAudioSource != null && !_isDead && gameObject.activeInHierarchy)
            {
                _ambientAudioSource.UnPause();
            }
        }

        public void EnableSound()
        {
            if (_ambientAudioSource != null && !_isDead)
            {
                if (!_isPaused)
                {
                    _ambientAudioSource.Play();
                }
            }
        }

        private void StopAmbient()
        {
            if (_ambientAudioSource != null)
            {
                _ambientAudioSource.Stop();
            }
        }

        public void HandleClick()
        {
            if (_isDead || _isPaused) return;

            _currentClicks++;
            RunBlinkRoutine().Forget();

            if (_clickSound != null)
            {
                _audioService.PlaySfx(_clickSound);
            }

            OnClickReceived?.Invoke();

            if (_currentClicks >= _clicksToOpen)
            {
                _isDead = true;
                StopAmbient();
                OnChestOpened?.Invoke();
            }
        }

        private async UniTaskVoid RunBlinkRoutine()
        {
            if (_animator == null) return;

            try
            {
                _animator.SetBool(_blinkBoolName, true);
                await UniTask.Delay(TimeSpan.FromSeconds(_blinkDuration),
                    cancellationToken: this.GetCancellationTokenOnDestroy());
                if (_animator != null)
                {
                    _animator.SetBool(_blinkBoolName, false);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void OnDeadAnimationEvent()
        {
            if (_deathSound != null)
            {
                _audioService.PlaySfx(_deathSound);
            }

            GiveRewardsAssortment();
            gameObject.SetActive(false);
        }

        private void GiveRewardsAssortment()
        {
            if (_chestRewards == null || _chestRewards.Length == 0 || _sceneRefs == null) return;

            var rewardList = new List<ResourceAmount>();

            foreach (var resSO in _chestRewards)
            {
                int amount = UnityEngine.Random.Range(_rewardRange.x, _rewardRange.y + 1);
                _resourcesSystem.AddResource(resSO, amount);
                rewardList.Add(new ResourceAmount(resSO, amount));
            }

            var popupAnchor = new GameObject("ChestRewardAnchor");
            popupAnchor.transform.position = transform.position;

            _sceneRefs.ResourceAmountAdded.ShowResourcesAmounts(
                rewardList.ToArray(),
                _sceneRefs.ResourcesView,
                popupAnchor.transform,
                Vector3.up * 0.5f
            ).Forget();

            Destroy(popupAnchor, 4f);
        }
    }
}