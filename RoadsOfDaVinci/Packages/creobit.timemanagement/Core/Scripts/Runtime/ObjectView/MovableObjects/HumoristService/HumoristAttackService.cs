using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Systems;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Creobit.Audio;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.HumoristService
{
    /// <summary>
    /// GG2 enemy attack behaviour. Only the attack itself lives here — patrol, spawn delay and the
    /// fireman lifecycle are fully data-driven via StealerActivationService + TimerService +
    /// MovableObjectAlgorithmService (same pattern as the resource stealer). The enemy's own
    /// movement animations (Run/Idle) are driven by MovableObjectView; this service only overlays a
    /// temporary Hit animation and applies the stun to the target.
    /// </summary>
    public class HumoristAttackService : MonoBehaviour
    {
        [SerializeField] private MovableObjectView _movableObjectView;
        [SerializeField] private HumoristAttackSettingsSO _settings;
        [SerializeField] private Collider _attackArea;

        private IGameplayIntervalsController _gameplayIntervals;
        private IAudioService _audioService;
        private MovableObjectView _target;
        private GameObject _stunEffect;
        private ushort? _stunIntervalId;
        private ushort? _cooldownIntervalId;
        private ushort? _hitTimeoutIntervalId;
        private bool _isAttacking;
        private bool _hitApplied;
        private bool _cooldownActive;

        public HumoristAttackSettingsSO Settings => _settings;
        public Collider AttackArea => _attackArea;
        public bool IsAttacking => _isAttacking;

        [Inject]
        private void Construct(IGameplayIntervalsController gameplayIntervals, IAudioService audioService)
        {
            _gameplayIntervals = gameplayIntervals;
            _audioService = audioService;
        }

        private void Awake()
        {
            _movableObjectView ??= GetComponent<MovableObjectView>();
        }

        private void OnValidate()
        {
            _movableObjectView ??= GetComponent<MovableObjectView>();
        }

        private void OnEnable()
        {
            // The enemy is re-activated by the Reboot timer for a fresh patrol — allow attacks again.
            if (_attackArea != null)
            {
                _attackArea.enabled = true;
            }
        }

        private void OnDisable()
        {
            // Release only this enemy's own state. The target's stun interval is global and keeps
            // running, so an already stunned unit is NOT freed early (its stun finishes normally).
            CancelHitTimeout();
            ReleaseSelf();
            _isAttacking = false;
            _hitApplied = false;
        }

        private void OnDestroy()
        {
            // Hard teardown (level unload): cancel everything and free the target so it can't stay frozen.
            CancelIntervals();
            ReleaseTarget();
        }

        /// <summary>Entry point from the child AttackArea trigger.</summary>
        public void TryAttack(Collider other)
        {
            if (other == null || _settings == null || _isAttacking || _cooldownActive ||
                _attackArea == null || !_attackArea.enabled)
            {
                return;
            }

            var target = other.GetComponentInParent<MovableObjectView>();
            if (!CanAttack(target))
            {
                return;
            }

            // Override the animation BEFORE locking movement. Acquiring the lock calls SetSpeed(),
            // which would otherwise re-raise the unit's Run trigger and pull it back into a
            // run-in-place pose during the wind-up. Setting the temporary animation first makes
            // PlayAnimation() a no-op (it is guarded while a temporary animation is active).
            if (!target.TryBeginTemporaryAnimation(this, _settings.IdleAnimationState))
            {
                return;
            }

            if (!_movableObjectView.TryBeginTemporaryAnimation(this, _settings.HitAnimationState))
            {
                target.EndTemporaryAnimation(this);
                return;
            }

            if (!target.TryAcquireMovementLock(this) || !_movableObjectView.TryAcquireMovementLock(this))
            {
                // Roll back any partially-acquired lock/animation so nothing stays frozen.
                _movableObjectView.ReleaseMovementLock(this);
                _movableObjectView.EndTemporaryAnimation(this);
                target.ReleaseMovementLock(this);
                target.EndTemporaryAnimation(this);
                return;
            }

            _target = target;
            _isAttacking = true;
            _hitApplied = false;
            TurnTowards(target.transform.position);
            StartHitTimeout();
        }

        /// <summary>Animation event on the Hit clip (~0.833s): the hit lands.</summary>
        public void StunObject()
        {
            ApplyHit();
        }

        /// <summary>Animation event on the Hit clip (~2.333s): the swing animation is over.</summary>
        public void StunAnimFinished()
        {
            if (!_isAttacking)
            {
                return;
            }

            // Defensive: if StunObject never fired, still land the hit so behaviour stays consistent.
            if (!_hitApplied)
            {
                ApplyHit();
            }

            FinishSwing();
        }

        /// <summary>
        /// Called from the prefab's onRegisterTask UltEvent, BEFORE ChangeAlgorithmGroup("WaitFireman").
        /// Stops new attacks and frees the enemy so it can walk to the wait point. An in-flight hit is
        /// finalized; an already active target stun is intentionally left running.
        /// </summary>
        public void PrepareForFireman()
        {
            if (_attackArea != null)
            {
                _attackArea.enabled = false;
            }

            if (_isAttacking && !_hitApplied)
            {
                ApplyHit();
            }

            FinishSwing();
        }

        /// <summary>
        /// Called from the prefab's onEndInteract / RunAway group. The enemy runs to its first point and
        /// reboots — handled by the algorithm group; here we just make sure it is no longer attacking.
        /// </summary>
        public void BeginRunAway()
        {
            if (_attackArea != null)
            {
                _attackArea.enabled = false;
            }

            CancelHitTimeout();
            ReleaseSelf();
            _isAttacking = false;
            _hitApplied = false;

            // Scared-run animation for the whole escape (original: Play("RunTerror")). As a temporary
            // animation it persists while the enemy moves — MovableObjectView's run animation is guarded
            // and won't override it. Cleared on disable when the enemy reboots.
            if (_movableObjectView != null && _settings != null &&
                !string.IsNullOrEmpty(_settings.RunAwayAnimationState))
            {
                _movableObjectView.TryBeginTemporaryAnimation(this, _settings.RunAwayAnimationState);
            }
        }

        private bool CanAttack(MovableObjectView target)
        {
            if (target == null || target == _movableObjectView || target.MovableObjectDataSO == null ||
                target.HasReachedTaskPoint || target.IsMovementLocked)
            {
                return false;
            }

            // Never target another enemy (a Humorist) regardless of tags.
            if (target.GetComponent<HumoristAttackService>() != null)
            {
                return false;
            }

            return target.MovableObjectDataSO.ObjectTypeTags.Contains(
                _settings.TargetTagsMode,
                _settings.TargetTags);
        }

        private void ApplyHit()
        {
            if (!_isAttacking || _hitApplied || _target == null || _settings == null)
            {
                return;
            }

            _hitApplied = true;
            _cooldownActive = true;

            _target.PlayTemporaryAnimation(this, _settings.GetReactionAnimation(_target));

            if (_settings.StunEffectPrefab != null)
            {
                var parent = _target.modelTransform != null ? _target.modelTransform : _target.transform;
                _stunEffect = Instantiate(_settings.StunEffectPrefab, parent);
                _stunEffect.transform.localPosition = _settings.StunEffectLocalPosition;
            }

            if (_settings.AttackSound != null && _audioService != null)
            {
                _audioService.PlaySfx(_settings.AttackSound);
            }

            StartStunAndCooldown();
        }

        /// <summary>Ends the enemy's swing: free its own movement/animation; the target stun continues.</summary>
        private void FinishSwing()
        {
            if (!_isAttacking)
            {
                return;
            }

            _isAttacking = false;
            CancelHitTimeout();
            ReleaseSelf();
        }

        private void ReleaseSelf()
        {
            if (_movableObjectView == null)
            {
                return;
            }

            _movableObjectView.EndTemporaryAnimation(this);
            _movableObjectView.ReleaseMovementLock(this);
        }

        private void StartStunAndCooldown()
        {
            CancelStunInterval();
            CancelCooldownInterval();

            if (_gameplayIntervals == null)
            {
                Debug.LogError($"{nameof(HumoristAttackService)} on {name} has no " +
                    $"{nameof(IGameplayIntervalsController)} injection.", this);
                _cooldownActive = false;
                ReleaseTarget();
                return;
            }

            _stunIntervalId = StartOneShot(Mathf.Max(0.1f, _settings.StunDurationSeconds), OnStunCompleted);
            _cooldownIntervalId = StartOneShot(Mathf.Max(0.1f, _settings.AttackCooldownSeconds), OnCooldownCompleted);
        }

        private void StartHitTimeout()
        {
            CancelHitTimeout();
            if (_gameplayIntervals == null)
            {
                return;
            }

            _hitTimeoutIntervalId = StartOneShot(Mathf.Max(0.1f, _settings.HitTimeoutSeconds), OnHitTimeout);
        }

        private ushort StartOneShot(float seconds, System.Action onCompleted)
        {
            var general = new GameplayIntervalGeneralParameters(seconds, seconds, 1);
            var specific = new GameplayIntervalSpecificParameters(
                null, null, null, null, null, null, null, onCompleted, null, null);
            return _gameplayIntervals.StartInterval(specific, general);
        }

        private void OnHitTimeout()
        {
            _hitTimeoutIntervalId = null;
            if (!_isAttacking)
            {
                return;
            }

            Debug.LogWarning($"{nameof(HumoristAttackService)} on {name}: Hit animation events did not " +
                $"fire within {_settings.HitTimeoutSeconds:0.##}s — force-finalizing the attack.", this);

            if (!_hitApplied)
            {
                ApplyHit();
            }

            FinishSwing();
        }

        private void OnStunCompleted()
        {
            _stunIntervalId = null;
            ReleaseTarget();
        }

        private void OnCooldownCompleted()
        {
            _cooldownIntervalId = null;
            _cooldownActive = false;
        }

        private void CancelIntervals()
        {
            CancelStunInterval();
            CancelCooldownInterval();
            CancelHitTimeout();
        }

        private void CancelStunInterval()
        {
            if (_stunIntervalId.HasValue && _gameplayIntervals != null)
            {
                _gameplayIntervals.CancelInterval(_stunIntervalId.Value);
            }

            _stunIntervalId = null;
        }

        private void CancelCooldownInterval()
        {
            if (_cooldownIntervalId.HasValue && _gameplayIntervals != null)
            {
                _gameplayIntervals.CancelInterval(_cooldownIntervalId.Value);
            }

            _cooldownIntervalId = null;
        }

        private void CancelHitTimeout()
        {
            if (_hitTimeoutIntervalId.HasValue && _gameplayIntervals != null)
            {
                _gameplayIntervals.CancelInterval(_hitTimeoutIntervalId.Value);
            }

            _hitTimeoutIntervalId = null;
        }

        private void ReleaseTarget()
        {
            if (_stunEffect != null)
            {
                Destroy(_stunEffect);
                _stunEffect = null;
            }

            if (_target != null)
            {
                _target.EndTemporaryAnimation(this);
                _target.ReleaseMovementLock(this);
                _target = null;
            }
        }

        private void TurnTowards(Vector3 targetPosition)
        {
            var model = _movableObjectView.modelTransform;
            if (model == null)
            {
                return;
            }

            var direction = targetPosition - transform.position;
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            var angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            model.rotation = _movableObjectView.initialRotation * Quaternion.AngleAxis(angle, Vector3.up);
        }
    }
}
