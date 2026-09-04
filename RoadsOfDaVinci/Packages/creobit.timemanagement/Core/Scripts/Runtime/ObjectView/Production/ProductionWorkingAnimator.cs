using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject
{
    public enum ProductionWorkingAnimationType
    {
        SquashStretch,
        Pulse,
        Bounce,
        AnkhaSway,
        AnchoredStretch
    }

    [Serializable]
    [InlineProperty]
    public sealed class ProductionWorkingAnimationSettings
    {
        [SerializeField]
        [ToggleLeft]
        private bool _enabled;

        [SerializeField]
        [ShowIf(nameof(_enabled))]
        private Transform _target;

        [SerializeField]
        [ShowIf(nameof(_enabled))]
        private ProductionWorkingAnimationType _type = ProductionWorkingAnimationType.SquashStretch;

        [SerializeField]
        [ShowIf(nameof(_enabled))]
        [Range(0f, 0.5f)]
        private float _strength = 0.08f;

        [SerializeField]
        [ShowIf(nameof(_enabled))]
        [Min(0.05f)]
        private float _cycleDuration = 0.6f;

        [SerializeField]
        [ShowIf("@_enabled && _type == ProductionWorkingAnimationType.Bounce")]
        [Min(0f)]
        private float _bounceHeight = 0.12f;

        [SerializeField]
        [ShowIf("@_enabled && (_type == ProductionWorkingAnimationType.AnkhaSway || " +
                "_type == ProductionWorkingAnimationType.AnchoredStretch)")]
        [Range(0f, 30f)]
        private float _swayAngle = 8f;

        [SerializeField]
        [ShowIf("@_enabled && _type == ProductionWorkingAnimationType.AnkhaSway")]
        [Min(0f)]
        private float _swayDistance = 0.08f;

        [SerializeField]
        [ShowIf("@_enabled && _type == ProductionWorkingAnimationType.AnchoredStretch")]
        [Range(0f, 0.5f)]
        private float _verticalStretch = 0.18f;

        [SerializeField]
        [ShowIf(nameof(_enabled))]
        [Range(0f, 1f)]
        private float _stopBlendDuration = 0.2f;

        public bool Enabled => _enabled;
        public Transform Target => _target;
        public ProductionWorkingAnimationType Type => _type;
        public float Strength => _strength;
        public float CycleDuration => _cycleDuration;
        public float BounceHeight => _bounceHeight;
        public float SwayAngle => _swayAngle;
        public float SwayDistance => _swayDistance;
        public float VerticalStretch => _verticalStretch;
        public float StopBlendDuration => _stopBlendDuration;
    }

    /// <summary>
    /// Runs only while production is animated or blending back to idle. A single UniTask state
    /// machine replaces a permanent MonoBehaviour.Update call on every static object.
    /// </summary>
    public sealed class ProductionWorkingAnimator : IDisposable
    {
        private readonly MonoBehaviour _owner;
        private readonly ProductionWorkingAnimationSettings _settings;
        private readonly Func<float> _speedProvider;
        private readonly CancellationToken _destroyCancellationToken;

        private bool _isRunning;
        private bool _isPlaying;
        private bool _isPaused;
        private bool _isStopping;
        private int _runVersion;
        private float _animationTime;
        private float _stopTime;

        private Transform _target;
        private Vector3 _baseScale;
        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private Vector3 _anchorLocal;
        private Vector3 _stopScale;
        private Vector3 _stopPosition;
        private Quaternion _stopRotation;

        public ProductionWorkingAnimator(
            MonoBehaviour owner,
            ProductionWorkingAnimationSettings settings,
            Func<float> speedProvider)
        {
            _owner = owner;
            _settings = settings;
            _speedProvider = speedProvider;
            _destroyCancellationToken = owner.GetCancellationTokenOnDestroy();
        }

        public void Start()
        {
            if (!_owner.isActiveAndEnabled || _settings == null || !_settings.Enabled ||
                _settings.Target == null)
            {
                ResetImmediately();
                return;
            }

            ResetImmediately();
            CaptureTarget(_settings.Target);

            _animationTime = 0f;
            _isPaused = false;
            _isStopping = false;
            _isPlaying = true;
            _isRunning = true;

            RunAsync(_runVersion).Forget();
        }

        public void Pause()
        {
            if (_isPlaying)
            {
                _isPaused = true;
            }
        }

        public void Resume()
        {
            if (_isPlaying)
            {
                _isPaused = false;
            }
        }

        public void Stop()
        {
            if (_target == null || _isStopping)
            {
                return;
            }

            if (!_owner.isActiveAndEnabled || _settings.StopBlendDuration <= 0f)
            {
                ResetImmediately();
                return;
            }

            _isPlaying = false;
            _isPaused = false;
            _animationTime = 0f;
            _stopTime = 0f;
            _stopScale = _target.localScale;
            _stopPosition = _target.localPosition;
            _stopRotation = _target.localRotation;
            _isStopping = true;
        }

        public void ResetImmediately()
        {
            RestoreTarget();

            _isRunning = false;
            _isPlaying = false;
            _isPaused = false;
            _isStopping = false;
            _animationTime = 0f;
            _stopTime = 0f;
            _target = null;
            _runVersion++;
        }

        public void Dispose()
        {
            ResetImmediately();
        }

        private async UniTaskVoid RunAsync(int version)
        {
            while (_isRunning && version == _runVersion && !_destroyCancellationToken.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, _destroyCancellationToken)
                    .SuppressCancellationThrow();

                if (!_isRunning || version != _runVersion || _destroyCancellationToken.IsCancellationRequested)
                {
                    return;
                }

                Tick(Time.deltaTime);
            }
        }

        private void Tick(float deltaTime)
        {
            if (_isStopping)
            {
                TickStop(deltaTime);
                return;
            }

            if (!_isPlaying || _isPaused)
            {
                return;
            }

            if (_settings == null || !_settings.Enabled || _settings.Target == null)
            {
                Stop();
                return;
            }

            if (_target != _settings.Target)
            {
                RestoreTarget();
                CaptureTarget(_settings.Target);
            }

            var duration = Mathf.Max(0.05f, _settings.CycleDuration);
            var speed = Mathf.Max(0f, _speedProvider?.Invoke() ?? 1f);
            if (speed <= 0f)
            {
                return;
            }

            _animationTime += deltaTime * speed;

            var normalizedTime = Mathf.Repeat(_animationTime / duration, 1f);
            var phase = normalizedTime * Mathf.PI * 2f;
            var wave = Mathf.Sin(phase);
            var strength = Mathf.Clamp(_settings.Strength, 0f, 0.5f);

            var scaleMultiplier = Vector3.one;
            var positionOffset = Vector3.zero;
            var rotationOffset = Quaternion.identity;
            var anchorBottomCenter = false;

            switch (_settings.Type)
            {
                case ProductionWorkingAnimationType.Pulse:
                    var pulse = 1f + wave * strength;
                    scaleMultiplier = new Vector3(pulse, pulse, 1f);
                    break;

                case ProductionWorkingAnimationType.Bounce:
                    var jump = 4f * normalizedTime * (1f - normalizedTime);
                    var bounceStretch = wave * strength * 0.5f;
                    scaleMultiplier = new Vector3(1f - bounceStretch, 1f + bounceStretch, 1f);
                    positionOffset = Vector3.up * (jump * _settings.BounceHeight);
                    break;

                case ProductionWorkingAnimationType.AnkhaSway:
                    var bodyBeat = Mathf.Sin(phase * 2f);
                    scaleMultiplier = new Vector3(
                        1f + bodyBeat * strength * 0.35f,
                        1f - bodyBeat * strength * 0.5f,
                        1f);
                    positionOffset = Vector3.right * (wave * _settings.SwayDistance);
                    rotationOffset = Quaternion.Euler(0f, 0f, -wave * _settings.SwayAngle);
                    break;

                case ProductionWorkingAnimationType.AnchoredStretch:
                    var sequenceTime = normalizedTime * 3f;
                    var sequenceIndex = Mathf.Min(2, Mathf.FloorToInt(sequenceTime));
                    var sequencePhase = sequenceTime - sequenceIndex;
                    var sequencePulse = Mathf.Sin(sequencePhase * Mathf.PI);
                    sequencePulse *= sequencePulse;

                    var lateralDirection = sequenceIndex switch
                    {
                        0 => 1f,
                        1 => -1f,
                        _ => 0f
                    };
                    var lateralPulse = sequencePulse * Mathf.Abs(lateralDirection);
                    var verticalPulse = sequencePulse * (sequenceIndex == 2 ? 1f : 0f);

                    scaleMultiplier = new Vector3(
                        1f - lateralPulse * strength * 0.25f - verticalPulse * strength * 0.6f,
                        1f + lateralPulse * strength * 0.45f + verticalPulse * _settings.VerticalStretch,
                        1f);
                    rotationOffset = Quaternion.Euler(
                        0f,
                        0f,
                        -lateralDirection * sequencePulse * _settings.SwayAngle);
                    anchorBottomCenter = true;
                    break;

                default:
                    scaleMultiplier = new Vector3(1f + wave * strength, 1f - wave * strength, 1f);
                    break;
            }

            ApplyPose(scaleMultiplier, positionOffset, rotationOffset, anchorBottomCenter);
        }

        private void ApplyPose(
            Vector3 scaleMultiplier,
            Vector3 positionOffset,
            Quaternion rotationOffset,
            bool anchorBottomCenter)
        {
            var animatedScale = Vector3.Scale(_baseScale, scaleMultiplier);
            var animatedRotation = _baseRotation * rotationOffset;
            var animatedPosition = _basePosition + positionOffset;

            if (anchorBottomCenter)
            {
                var baseAnchorPosition = _basePosition +
                                         _baseRotation * Vector3.Scale(_baseScale, _anchorLocal);
                animatedPosition = baseAnchorPosition -
                                   animatedRotation * Vector3.Scale(animatedScale, _anchorLocal);
            }

            _target.localScale = animatedScale;
            _target.localPosition = animatedPosition;
            _target.localRotation = animatedRotation;
        }

        private void CaptureTarget(Transform target)
        {
            _target = target;
            _baseScale = target.localScale;
            _basePosition = target.localPosition;
            _baseRotation = target.localRotation;

            var spriteRenderer = target.GetComponent<SpriteRenderer>();
            _anchorLocal = spriteRenderer != null && spriteRenderer.sprite != null
                ? new Vector3(spriteRenderer.sprite.bounds.center.x, spriteRenderer.sprite.bounds.min.y, 0f)
                : Vector3.zero;
        }

        private void TickStop(float deltaTime)
        {
            if (_target == null)
            {
                ResetImmediately();
                return;
            }

            var duration = Mathf.Max(0.01f, _settings.StopBlendDuration);
            _stopTime += deltaTime;
            var normalizedTime = Mathf.Clamp01(_stopTime / duration);
            var easedTime = 1f - Mathf.Pow(1f - normalizedTime, 3f);

            _target.localScale = Vector3.LerpUnclamped(_stopScale, _baseScale, easedTime);
            _target.localPosition = Vector3.LerpUnclamped(_stopPosition, _basePosition, easedTime);
            _target.localRotation = Quaternion.SlerpUnclamped(_stopRotation, _baseRotation, easedTime);

            if (normalizedTime >= 1f)
            {
                ResetImmediately();
            }
        }

        private void RestoreTarget()
        {
            if (_target == null)
            {
                return;
            }

            _target.localScale = _baseScale;
            _target.localPosition = _basePosition;
            _target.localRotation = _baseRotation;
        }
    }
}
