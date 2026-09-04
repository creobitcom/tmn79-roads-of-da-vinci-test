using System;
using System.Collections.Generic;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.Logger;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map
{
/// <summary>
/// Represents a view component for a map page that manages map spot views.
/// </summary>
public class MapPageView : MonoBehaviour
{
    [SerializeField] 
    private List<MapSpotView> _mapSpotViews = new();

    /// <summary>
    /// Gets the collection of map spot views.
    /// </summary>
    public IReadOnlyList<MapSpotView> MapSpots => _mapSpotViews;
    
    [Serializable]
    public struct PackLevel
    {
        [field: SerializeField] public int level {get; private set; }
        [field: SerializeField] public GameObject PackObject {get; private set; }
    }
    
    [SerializeField] private List<PackLevel> _packLevels = new();
    [SerializeField] private Sprite _unlockedLvlSprite;
    [SerializeField] private Sprite _blockedLvlSprite;
    
    [field: SerializeField] public bool EnableScroll { get; private set; }
    
    [SerializeField, ShowIf(nameof(EnableScroll))] private GameObject _upArrow;
    [SerializeField, ShowIf(nameof(EnableScroll))] private GameObject _downArrow;
    [SerializeField, ShowIf(nameof(EnableScroll))] private float _arrowFadeDuration = 0.28f;
    [SerializeField, ShowIf(nameof(EnableScroll))] private Ease _arrowFadeEase = Ease.InOutSine;
    
    [SerializeField, ShowIf(nameof(EnableScroll))] private Transform _mapTransform;
    [SerializeField, ShowIf(nameof(EnableScroll))] private float _speed;
    [SerializeField, ShowIf(nameof(EnableScroll))] private float _minMapPosY;
    [SerializeField, ShowIf(nameof(EnableScroll))] private float _maxMapPosY;
    
    [SerializeField, ShowIf(nameof(EnableScroll))] private LayerMask mask;
    [SerializeField, ShowIf(nameof(EnableScroll))] private List<Canvas> canvases;
    [SerializeField, ShowIf(nameof(EnableScroll))] private float dragBorder = 10F;

    private const float ReferenceDpi = 96F;

    private float ScaledDragBorder =>
        Screen.dpi > 0F
            ? dragBorder * Mathf.Max(1F, Screen.dpi / ReferenceDpi)
            : dragBorder;
    
    //private MetaSceneReferences _metaSceneReferences;
    [SerializeField] private GameObject MapFlag;
    [SerializeField] private bool isBonusMap;
    
    private Vector2 _pressPosition;
    private bool _pressStartedOnMap;
    private bool _isDragging;
    private float _lastMapPos = float.MinValue;
    private CanvasGroup _upArrowGroup;
    private CanvasGroup _downArrowGroup;
    private Tween _upArrowFade;
    private Tween _downArrowFade;
    private bool _upArrowShown;
    private bool _downArrowShown;

    private const string MAP_POS = "MAP_POS";
    private const string MAP_POS_BONUS = "MAP_POS_BONUS";
    private bool IsUpScrolling{ get; set; }
    private bool IsDownScrolling{ get; set; }
    
    private ISaveController _saveController;
    private IMetaInputSystem _inputSystem;
    
    [Inject]
    private void Construct(ISaveController saveController,
        IMetaInputSystem inputSystem)
    {
        _saveController = saveController;
        _inputSystem = inputSystem;
    }
    
    private void Awake()
    {
        if (_mapSpotViews == null || _mapSpotViews.Count == 0)
        {
            Log.Meta.Warning($"No MapSpotView components found in children of {gameObject.name}");
        }
        
        if (!EnableScroll)
        {
            return;
        }

        if (PlayerPrefs.HasKey(MapPosKey))
        {
            _lastMapPos = Convert.ToSingle(PlayerPrefs.GetString(MapPosKey));
        }

        if (Mathf.Approximately(_lastMapPos, float.MinValue)) {
            _mapTransform.position = new Vector3(0f, _minMapPosY, 0f);
        } else {
            _mapTransform.position = new Vector3(0f, ClampMapPosY(_lastMapPos), 0f);
        }
    }
    
    [ContextMenu("TEST")]
    private void Start()
    {
        var lastUnlockedLevel = _saveController.CurrentSaveData.LastUnlockedLevel;

        foreach (var packLevel in _packLevels)
        {
            Log.Meta.Info("LastUnlockedLevel: " + lastUnlockedLevel);
            packLevel.PackObject.gameObject.SetActive(packLevel.level <= lastUnlockedLevel);
        }

        // foreach (var spotView in _mapSpotViews)
        // {
        //     spotView.image.sprite = spotView.levelNum <=lastUnlockedLevel ? _unlockedLvlSprite : _blockedLvlSprite;
        // }
        
        if (!EnableScroll)
        {
            return;
        }

        MoveCamera(lastUnlockedLevel >= _mapSpotViews.Count ? _mapSpotViews.Count : lastUnlockedLevel);
    }

    public void ScrollToFlag()
    {
            var targetPos = Camera.main.ScreenToWorldPoint(
                new Vector3(Screen.width / 2, Screen.height / 2, 10));
                    
            var targetY = (transform.position.y - MapFlag.transform.position.y)
                          - targetPos.y;

            if (EnableScroll)
            {
                targetY = ClampMapPosY(targetY);
            }
                    
            transform.position = new Vector3(
                transform.position.x, 
                targetY, 
                transform.position.z);
    }

    private float ClampMapPosY(float y)
    {
        return Mathf.Clamp(y, _minMapPosY, _maxMapPosY);
    }

    private string MapPosKey => isBonusMap ? MAP_POS_BONUS : MAP_POS;

    private void SaveMapPos()
    {
        if (!EnableScroll || _mapTransform == null)
        {
            return;
        }

        PlayerPrefs.SetString(MapPosKey, _mapTransform.position.y.ToString());
        PlayerPrefs.Save();
    }
    
    private void OnDestroy()
    {
        KillArrowFades();
        SaveMapPos();
    }

    private void OnEnable()
    {
        StopArrowScrolling();
        _upArrowShown = false;
        _downArrowShown = false;
    }

    private void OnDisable()
    {
        StopArrowScrolling();
        KillArrowFades();
        SaveMapPos();
    }

    private void StopArrowScrolling()
    {
        IsUpScrolling = false;
        IsDownScrolling = false;
    }

    private void KillArrowFades()
    {
        _upArrowFade?.Kill();
        _downArrowFade?.Kill();
        _upArrowFade = null;
        _downArrowFade = null;
    }

    private void SetArrowVisible(
        GameObject arrow,
        ref CanvasGroup group,
        ref Tween tween,
        ref bool shown,
        bool shouldShow)
    {
        if (arrow == null || shown == shouldShow)
        {
            return;
        }

        shown = shouldShow;
        tween?.Kill();

        if (group == null)
        {
            group = arrow.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = arrow.AddComponent<CanvasGroup>();
            }
        }

        if (shouldShow)
        {
            if (!arrow.activeSelf)
            {
                group.alpha = 0f;
                arrow.SetActive(true);
            }

            group.blocksRaycasts = true;
            group.interactable = true;

            if (_arrowFadeDuration <= 0f)
            {
                group.alpha = 1f;
                tween = null;
                return;
            }

            tween = group.DOFade(1f, _arrowFadeDuration).SetEase(_arrowFadeEase).SetTarget(group);
            return;
        }

        group.blocksRaycasts = false;
        group.interactable = false;

        if (_arrowFadeDuration <= 0f || !arrow.activeSelf)
        {
            group.alpha = 0f;
            arrow.SetActive(false);
            tween = null;
            return;
        }

        var target = arrow;
        tween = group.DOFade(0f, _arrowFadeDuration)
            .SetEase(_arrowFadeEase)
            .SetTarget(group)
            .OnComplete(() =>
            {
                if (target != null)
                {
                    target.SetActive(false);
                }
            });
    }
    
    //TODO Перевести на новую InputSystem
    //TODO Нормализовать движение для всех устройств и видов ввода
    private void Update()
    {
        if (!EnableScroll)
        {
            return;
        }

        if (!PointerHeld())
        {
            StopArrowScrolling();
        }
        
        if (_mapTransform.position.y <= _minMapPosY) 
        {
            SetArrowVisible(_upArrow, ref _upArrowGroup, ref _upArrowFade, ref _upArrowShown, false);
            IsUpScrolling = false;
        } 
        else 
        {
            SetArrowVisible(_upArrow, ref _upArrowGroup, ref _upArrowFade, ref _upArrowShown, true);
        }
        if (_mapTransform.position.y >= _maxMapPosY) 
        {
            SetArrowVisible(_downArrow, ref _downArrowGroup, ref _downArrowFade, ref _downArrowShown, false);
            IsDownScrolling = false;
        } 
        else 
        {
            SetArrowVisible(_downArrow, ref _downArrowGroup, ref _downArrowFade, ref _downArrowShown, true);
        }
        if (IsUpScrolling) 
        {
            Move(1f);
        }
        if (IsDownScrolling) 
        {
            Move(-1f);
        }

        UpdateDragState();

        HandleDrag();

        HandleScroll();
    }

    /// <summary>
    /// Tracks where the current press began. Scrolling is only allowed when the press landed
    /// on empty map: pressing a level spot or a button means the player is interacting with
    /// that thing, and dragging away from it must not drag the map with it.
    /// </summary>
    private void UpdateDragState()
    {
        if (PointerPressedThisFrame())
        {
            _pressPosition = PointerPosition();
            _pressStartedOnMap = !UIHelper.IsPointerOverBlockingUI(_pressPosition)
                                 && !IsPointerOverClickableObject(_pressPosition);
            _isDragging = false;
        }

        if (PointerReleasedThisFrame())
        {
            _pressStartedOnMap = false;
            _isDragging = false;
        }
    }

    /// <summary>
    /// True when the press landed on a clickable world object — a level spot. Those are
    /// sprites with colliders rather than UI, so <see cref="UIHelper"/> cannot see them.
    /// </summary>
    private static bool IsPointerOverClickableObject(Vector2 screenPosition)
    {
        var camera = Camera.main;

        if (camera == null)
        {
            return false;
        }

        if (!Physics.Raycast(camera.ScreenPointToRay(screenPosition), out var hit))
        {
            return false;
        }

        return hit.collider.GetComponent<IReactToClick>() != null;
    }

    // The old UnityEngine.Input API throws InvalidOperationException on every call when
    // the project runs with the new Input System as the active handler (it did, every
    // frame, flooding the log and lagging the editor). These helpers read the same
    // values through the Input System and no-op when no device is present.
    // Mouse and touch are both handled: a phone has no Mouse device at all, so gating the
    // drag on the mouse alone left touch scrolling dead.
    private static bool PointerPressedThisFrame()
        => (Mouse.current?.leftButton.wasPressedThisFrame ?? false)
           || (Touchscreen.current?.primaryTouch.press.wasPressedThisFrame ?? false);

    private static bool PointerReleasedThisFrame()
        => (Mouse.current?.leftButton.wasReleasedThisFrame ?? false)
           || (Touchscreen.current?.primaryTouch.press.wasReleasedThisFrame ?? false);

    private static bool PointerHeld()
        => (Mouse.current?.leftButton.isPressed ?? false)
           || (Touchscreen.current?.primaryTouch.press.isPressed ?? false);

    private static Vector2 PointerPosition()
    {
        var touchscreen = Touchscreen.current;

        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
        {
            return touchscreen.primaryTouch.position.ReadValue();
        }

        return Mouse.current?.position.ReadValue() ?? Vector2.zero;
    }

    private static Vector2 MouseDelta() => Mouse.current?.delta.ReadValue() ?? Vector2.zero;

    private static float TouchDeltaY()
    {
        var touchscreen = Touchscreen.current;

        if (touchscreen == null || !touchscreen.primaryTouch.press.isPressed)
        {
            return float.NaN;
        }

        return touchscreen.primaryTouch.delta.ReadValue().y;
    }

    private static float ScrollNotches()
    {
        var raw = Mouse.current?.scroll.ReadValue().y ?? 0f;

        if (raw == 0f)
        {
            return 0f;
        }

        // The Input System reports wheel scroll in device units that differ by platform
        // and package version (±120 per notch on Windows raw, ±1 normalized, small floats
        // on macOS trackpads). Treat every event as at least one notch and map onto the
        // legacy GetAxis scale (~0.1 per notch) that all the speed constants were tuned
        // for — a fixed divisor made scrolling nearly dead outside one configuration.
        var magnitude = Mathf.Abs(raw);
        var notches = magnitude >= 60f ? magnitude / 120f : 1f;

        return Mathf.Sign(raw) * 0.1f * notches;
    }

    private void HandleScroll()
    {
        var scrollDelta = ScrollNotches();

        if (scrollDelta == 0) return;
        
#if UNITY_STANDALONE_OSX
        var speed = scrollDelta * 14f;
#else
        var maxScrollDelta = 0.2f;

        if(Mathf.Abs(scrollDelta) > maxScrollDelta) 
        {
            scrollDelta = Mathf.Sign(scrollDelta) * maxScrollDelta;
        }

        var speed = scrollDelta * 50f;
#endif

        Move(speed);
    }

    private void HandleDrag()
    {
        if (!PointerHeld() || !_pressStartedOnMap) return;

        // Require a deliberate drag before scrolling. Without this the pointer jitter of an
        // ordinary click was enough to slide the map, so pressing a spot felt like a drag.
        if (!_isDragging)
        {
            if (Vector2.Distance(PointerPosition(), _pressPosition) < ScaledDragBorder)
            {
                return;
            }

            _isDragging = true;
        }

        var touchDeltaY = TouchDeltaY();
        var drag = !float.IsNaN(touchDeltaY) ? touchDeltaY / 7f : MouseDelta().y;

#if UNITY_STANDALONE_WIN
        Move(-drag / 5.5f);
#else
        Move(-drag / 2.5f);
#endif
    }

    private void Move(float acceleration)
    {
        if (!EnableScroll)
        {
            return;
        }
        
        acceleration = -acceleration;
        
        var mapPosition = _mapTransform.position.y + _speed * acceleration * Time.deltaTime;

        if (mapPosition < _minMapPosY)
        {
            _mapTransform.position = new Vector3(0f, _minMapPosY, 0f);
            return;
        }
        
        if (mapPosition > _maxMapPosY) 
        {
            _mapTransform.position = new Vector3(0f, _maxMapPosY, 0f);
            return;
        }

        var newPosition = _mapTransform.position + Vector3.up * (_speed * acceleration * Time.deltaTime);
        _mapTransform.position = newPosition;
    }

    private void MoveCamera(int levelButton) {
    if(EnableScroll)       
        _mapTransform.position = new Vector3(0f, Mathf.Clamp(_mapSpotViews[levelButton-1].transform.localPosition.y * -1f, _minMapPosY, _maxMapPosY), 0f);
    }
    
    public bool Raycast(Vector2 pos, LayerMask layerMask, List<Canvas> canvases, bool overray = false) {
        List<GraphicRaycaster> graphicsRaycasters = new List<GraphicRaycaster>();

        foreach (Canvas c in canvases) {
            if (c.GetComponent<GraphicRaycaster>())
                graphicsRaycasters.Add(c.GetComponent<GraphicRaycaster>());
        }

        PointerEventData ped = new PointerEventData(null);
        ped.position = pos;
        List<RaycastResult> results = new List<RaycastResult>();

        foreach (GraphicRaycaster gr in graphicsRaycasters) {
            gr.Raycast(ped, results);
        }

        bool result = false;

        foreach (RaycastResult rr in results) {
            if (overray) {
                if (layerMask. IsInLayerMask(rr.gameObject.layer))
                    return true;
            } else {
                if (layerMask.IsInLayerMask(rr.gameObject.layer))
                    result = true;
                else return false;
            }
        }

        return result;
    }
    
    
    /// <summary>
    /// Initializes the map spots collection. Can be called manually if needed.
    /// </summary>
    [Button("Initialize Map Spots")]
    public void InitializeMapSpots()
    {
        var spotViews = GetComponentsInChildren<MapSpotView>();
        if (spotViews == null || spotViews.Length == 0)
        {
            Log.Meta.Warning($"No MapSpotView components found in children of {gameObject.name}");
            return;
        }

        _mapSpotViews = spotViews.ToList();
    }

    /// <summary>
    /// Gets the total number of levels on this map page.
    /// </summary>
    /// <returns>The count of map spot views.</returns>
    public int GetLevelsCount()
    {
        if (_mapSpotViews == null || _mapSpotViews.Count == 0)
        {
            return 0;
        }
        
        return _mapSpotViews.Count;
    }
}
}

public static class LayerMaskExtensions {
    public static bool IsInLayerMask(this LayerMask layermask, int layer) {
        return layermask == (layermask | (1 << layer));
    }
}