using UnityEngine;
using System.Collections.Generic;
using Creobit.Audio;
using DG.Tweening;
using VContainer;

public class CableRailway : MonoBehaviour
{
    private enum MovementMode
    {
        LegacyAnimator,
        DynamicTween
    }

    [SerializeField] private float detectionRadius = 1f;
    [SerializeField] private AudioClip _cablewayEntrySound;

    [Header("Movement")]
    [SerializeField] private MovementMode _movementMode = MovementMode.LegacyAnimator;
    [SerializeField] private Transform _cabin;
    [SerializeField] private Transform _leftCabinStop;
    [SerializeField] private Transform _rightCabinStop;
    [SerializeField] private Vector3 _leftCabinStopOffset;
    [SerializeField] private Vector3 _rightCabinStopOffset;
    [SerializeField, Min(0.01f)] private float _cabinMoveSpeed = 2.5f;
    [SerializeField] private Ease _cabinMoveEase = Ease.InOutSine;
    [SerializeField, Min(0f)] private float _stationWaitDuration;

    public static CableRailway Instance;

    public Transform leftStationPoint;
    public Transform rightStationPoint;

    public bool isRestored;

    private readonly List<GameObject> _waitingOnLeft = new();
    private readonly List<GameObject> _waitingOnRight = new();

    private IAudioService _audioService;
    private Animator _legacyAnimator;
    private Tween _cabinTween;
    private bool _isCabinAtLeft;
    private bool _isStarted;
    private bool _lastRestoredState;

    [Inject]
    public void Construct(IAudioService audioService)
    {
        _audioService = audioService;
    }
    
    private void Awake()
    {
        Instance = this;
        if (_cabin == null) _cabin = transform;
        _legacyAnimator = _cabin.GetComponent<Animator>();
    }

    private void Start()
    {
        _isStarted = true;
        _lastRestoredState = isRestored;
        RefreshMovement();
    }

    private void Update()
    {
        if (_lastRestoredState == isRestored) return;

        _lastRestoredState = isRestored;
        RefreshMovement();
    }

    private void OnEnable()
    {
        if (_isStarted) RefreshMovement();
    }

    private void OnDisable()
    {
        StopDynamicMovement();
    }

    private void OnDestroy()
    {
        StopDynamicMovement();
        if (Instance == this) Instance = null;
    }

    public bool TryRegisterLandedUnit(GameObject unit, Vector3 landingPosition)
    {
        var sqrRadius = detectionRadius * detectionRadius;

        var distToLeft = Vector3.SqrMagnitude(landingPosition - leftStationPoint.position);
        var distToRight = Vector3.SqrMagnitude(landingPosition - rightStationPoint.position);

        if (distToLeft < sqrRadius || distToRight < sqrRadius)
        {
            if (distToLeft < sqrRadius) _waitingOnLeft.Add(unit);
            else _waitingOnRight.Add(unit);

            if (_cablewayEntrySound != null && _audioService != null)
            {
                _audioService.PlaySfx(_cablewayEntrySound);
            }
            return true;
        }

        return false;
    }

    public void OnCabinReachedLeft() => ReleaseUnits(_waitingOnLeft);
    public void OnCabinReachedRight() => ReleaseUnits(_waitingOnRight);

    private void RefreshMovement()
    {
        if (!_isStarted || _cabin == null) return;

        StopDynamicMovement();

        var useDynamicMovement = _movementMode == MovementMode.DynamicTween;
        if (_legacyAnimator != null) _legacyAnimator.enabled = !useDynamicMovement;

        if (!useDynamicMovement || !isRestored) return;

        var leftStop = GetCabinStopPosition(_leftCabinStop, leftStationPoint, _leftCabinStopOffset);
        var rightStop = GetCabinStopPosition(_rightCabinStop, rightStationPoint, _rightCabinStopOffset);
        if (!leftStop.HasValue || !rightStop.HasValue)
        {
            Debug.LogWarning("CableRailway requires both cabin stop points for DynamicTween mode.", this);
            return;
        }

        var distanceToLeft = Vector3.SqrMagnitude(_cabin.position - leftStop.Value);
        var distanceToRight = Vector3.SqrMagnitude(_cabin.position - rightStop.Value);
        _isCabinAtLeft = distanceToLeft <= distanceToRight;

        MoveToNextStation();
    }

    private void MoveToNextStation()
    {
        if (!isActiveAndEnabled || !isRestored || _movementMode != MovementMode.DynamicTween) return;

        var destination = _isCabinAtLeft
            ? GetCabinStopPosition(_rightCabinStop, rightStationPoint, _rightCabinStopOffset)
            : GetCabinStopPosition(_leftCabinStop, leftStationPoint, _leftCabinStopOffset);
        if (!destination.HasValue) return;

        _cabinTween = _cabin.DOMove(destination.Value, Mathf.Max(_cabinMoveSpeed, 0.01f))
            .SetSpeedBased()
            .SetEase(_cabinMoveEase)
            .SetDelay(_stationWaitDuration)
            .OnComplete(() =>
            {
                _isCabinAtLeft = !_isCabinAtLeft;

                if (_isCabinAtLeft) OnCabinReachedLeft();
                else OnCabinReachedRight();

                MoveToNextStation();
            });
    }

    private static Vector3? GetCabinStopPosition(Transform explicitStop, Transform stationPoint, Vector3 offset)
    {
        if (explicitStop != null) return explicitStop.position;
        if (stationPoint != null) return stationPoint.position + offset;
        return null;
    }

    private void StopDynamicMovement()
    {
        _cabinTween?.Kill();
        _cabinTween = null;
    }

    private void ReleaseUnits(List<GameObject> units)
    {
        foreach (var unit in units)
        {
            if (unit != null && unit.TryGetComponent<UnitVisuals>(out var visuals))
            {
                visuals.Realise();
            }
        }

        units.Clear();
    }
}
