using System;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.CollectionRoom;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using Log = Creobit.Logger.Log;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem
{
    public class MetaInputSystem : IMetaInputSystem
    {
        private Camera _metaCamera;
        private GameplayInputActions _gameplayInputActions;
        private InputAction _primaryAction;
        private InputAction _secondaryAction;
        private InputAction _cursorPositionAction;
        private InputAction _backAction;
#if CREOBIT_DEBUG
        private InputAction _debugCloseAction;
#endif
        private Vector2 _cursorPosition;
        private bool _hoverIsActive;
        private bool _hoverDelayStarted;
        private IReactToHover _hoverObjectView;
#if UNITY_EDITOR
        private _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map.MapSpotView _lastHoveredSpot;
#endif
        private GameDevice _activeGameDevice;

        public event Action OnMainButtonPressed;
        public event Action OnBackButtonPressed;
        public event Action OnGameDeviceChanged;
        public event Action<Vector2> OnSecondaryActionHold;
        
        // New events for vertical swipe detection
        public event Action<float> OnSwipeVertical;
        
        // Pointer travel, in pixels, that still counts as a click rather than a map drag.
        private const float ClickMoveTolerance = 10f;
        private const float ReferenceDpi = 96f;

        private static float ScaledClickMoveTolerance =>
            Screen.dpi > 0f
                ? ClickMoveTolerance * Mathf.Max(1f, Screen.dpi / ReferenceDpi)
                : ClickMoveTolerance;

        // Swipe detection configuration variables
        private float _minSwipeDistance = 20f; // Minimum distance in pixels to consider a swipe
        private float _maxSwipeTime = 0.5f; // Maximum time in seconds to consider a swipe
        private bool _debugMode = false;
        
        // Swipe detection variables
        private Vector2 _startPosition;
        private float _startTime;
        private bool _isSwiping = false;

        public UniTask Load(Camera camera)
        {
            _metaCamera = camera;
            _gameplayInputActions = new GameplayInputActions();
            _primaryAction = _gameplayInputActions.TimeManagement.PrimaryAction;
            _secondaryAction = _gameplayInputActions.TimeManagement.SecondaryAction;
            _cursorPositionAction = _gameplayInputActions.TimeManagement.CursorPosition;
            _backAction = _gameplayInputActions.TimeManagement.BackAction;
#if CREOBIT_DEBUG
            _debugCloseAction = _gameplayInputActions.TimeManagement.DebugCloseGame;
#endif
            Enable();
            return UniTask.CompletedTask;
        }

        public void Enable()
        {
            Log.Gameplay.Info("MetaInputSystem.Enable");
            InputSystem.onActionChange += InputSystemOnActionChange;
            _primaryAction.performed += OnPrimaryActionPerformed;
            _primaryAction.started += OnPrimaryActionStarted;
            _primaryAction.canceled += OnPrimaryActionCanceled;
            _secondaryAction.performed += OnSecondaryActionPerformed;
            _secondaryAction.canceled += OnSecondaryActionCancelled;
            _cursorPositionAction.performed += OnCursorPositionChanged;
            _backAction.performed += OnBackButtonPerformed;
#if CREOBIT_DEBUG
            _debugCloseAction.Enable();
#endif
            _primaryAction.Enable();
            _secondaryAction.Enable();
            _cursorPositionAction.Enable();
            _backAction.Enable();
        }

        private void OnBackButtonPerformed(InputAction.CallbackContext obj)
        {
            OnBackButtonPressed?.Invoke();
            
            Log.Meta.Info("OnBackButtonPerformed");
        }

        public void Disable()
        {
            Log.Gameplay.Info("MetaInputSystem.Disable");
            InputSystem.onActionChange -= InputSystemOnActionChange;
            _primaryAction.performed -= OnPrimaryActionPerformed;
            _primaryAction.started -= OnPrimaryActionStarted;
            _primaryAction.canceled -= OnPrimaryActionCanceled;
            _secondaryAction.performed -= OnSecondaryActionPerformed;
            _secondaryAction.canceled -= OnSecondaryActionCancelled;
            _cursorPositionAction.performed -= OnCursorPositionChanged;
            _backAction.performed -= OnBackButtonPerformed;
#if CREOBIT_DEBUG
            _debugCloseAction.Disable();
#endif
            _primaryAction.Disable();
            _secondaryAction.Disable();
            _cursorPositionAction.Disable();
            _backAction.Disable();
        }
        
        private void InputSystemOnActionChange(object obj, InputActionChange inputActionChange)
        {
            if (inputActionChange != InputActionChange.ActionPerformed || obj is not InputAction inputAction)
            {
                return;
            }

            if (inputAction.activeControl.device.displayName == "VirtualMouse")
            {
                return;
            }

            if (inputAction.activeControl.device is Gamepad)
            {
                if (_activeGameDevice != GameDevice.Gamepad)
                {
                    ChangeActiveGameDevice(GameDevice.Gamepad);
                }
            }
            else
            {
                if (_activeGameDevice != GameDevice.MouseAndTouch)
                {
                    ChangeActiveGameDevice(GameDevice.MouseAndTouch);
                }
            }
        }

        private void ChangeActiveGameDevice(GameDevice gameDevice)
        {
            _activeGameDevice = gameDevice;
            
            Log.Gameplay.Info($"New active game devise is {_activeGameDevice}");
            Cursor.visible = _activeGameDevice == GameDevice.MouseAndTouch;
            
            OnGameDeviceChanged?.Invoke();
        }
        
        public GameDevice GetActiveGameDevice()
        {
            return _activeGameDevice;
        }

        private void OnSecondaryActionPerformed(InputAction.CallbackContext obj)
        {
            _hoverIsActive = true;

            if(!Physics.Raycast(_metaCamera.ScreenPointToRay(_cursorPosition), out var hit))
            {
                return;
            }

            var reactTo = hit.collider.GetComponent<IReactToHover>();

            if(_hoverObjectView is null || !_hoverObjectView.Equals(reactTo))
            {
                _hoverObjectView?.OnHoverEnd();
                _hoverObjectView = reactTo;
            }

            if(_hoverObjectView is null)
                return;

            _hoverObjectView.OnHover();
        }

        private void OnSecondaryActionCancelled(InputAction.CallbackContext obj)
        {
            _hoverIsActive = false;

            if(_hoverObjectView is null)
            {
                return;
            }

            _hoverObjectView.OnHoverEnd();

            _hoverObjectView = null;
        }

        private void OnCursorPositionChanged(InputAction.CallbackContext obj)
        {
            _cursorPosition = obj.ReadValue<Vector2>();

            if (_hoverIsActive)
            {
                OnSecondaryActionHold?.Invoke(_cursorPosition);
            }

            if(!Physics.Raycast(_metaCamera.ScreenPointToRay(_cursorPosition), out var hit))
            {
#if UNITY_EDITOR
                _lastHoveredSpot = null;
#endif
                OnSecondaryActionCancelled(obj);
                return;
            }

#if UNITY_EDITOR
            var spotView = hit.collider.GetComponent<_8floor.TimeManagement.Core.Scripts.Runtime.UI.Map.MapSpotView>();
            if (spotView != _lastHoveredSpot)
            {
                _lastHoveredSpot = spotView;
                if (spotView != null)
                {
                    Debug.LogError($"[MapSpot] Hover level: {spotView.levelNum} (interactable: {spotView.Interactable})");
                }
            }
#endif

            var reactTo = hit.collider.GetComponent<IReactToHover>();

            if(_hoverObjectView != reactTo)
            {
                _hoverObjectView?.OnHoverEnd();
                _hoverObjectView = reactTo;
                _hoverDelayStarted = false;
            }

            if(_hoverObjectView is null)
            {
                return;
            }

            if(!_hoverDelayStarted)
            {
                _hoverDelayStarted = true;
                _hoverObjectView.OnHover();
            }
        }

        // Handler for when primary action (mouse/touch) begins
        private void OnPrimaryActionStarted(InputAction.CallbackContext obj)
        {
            if (_cursorPositionAction != null)
            {
                _cursorPosition = _cursorPositionAction.ReadValue<Vector2>();
            }

            _startPosition = _cursorPosition;
            _startTime = Time.time;
            _isSwiping = true;
            
            if (_debugMode)
            {
                Log.Gameplay.Info($"Swipe started at: {_startPosition}");
            }
        }
        
        // Handler for when primary action (mouse/touch) is released
        private void OnPrimaryActionCanceled(InputAction.CallbackContext obj)
        {
            if (_isSwiping)
            {
                DetectSwipe();
                _isSwiping = false;
            }
        }

        private void OnPrimaryActionPerformed(InputAction.CallbackContext obj)
        {
            OnMainButtonPressed?.Invoke();

            // A click that lands on a button (Play, map arrows) must not also fall through to
            // the world: the map spots sit behind that UI, so without this the Play press
            // re-selects whatever spot is underneath it and starts that level instead of the
            // one the player picked.
            if (UIHelper.IsPointerOverBlockingUI(_cursorPosition))
            {
                return;
            }

            // Scrolling the map is a press-move-release, which ends in exactly the same
            // callback as a click. Only treat it as a click if the pointer stayed put;
            // the tolerance matches MapPageView.dragBorder, which starts the scroll.
            if (Vector2.Distance(_cursorPosition, _startPosition) > ScaledClickMoveTolerance)
            {
                return;
            }

            if (!Physics.Raycast(_metaCamera.ScreenPointToRay(_cursorPosition), out var hit))
            {
                return;
            }

            var reactTo = hit.collider.GetComponents<IReactToClick>();
            foreach (var reactToClick in reactTo)
            {
                reactToClick?.OnClick();
            }
        }

        // Detect and process swipe gestures
        private void DetectSwipe()
        {
            // Calculate time and distance
            float duration = Time.time - _startTime;
            Vector2 direction = _cursorPosition - _startPosition;
            float distance = direction.magnitude;
            
            // Check if this is a valid swipe (fast enough and long enough)
            if (duration <= _maxSwipeTime && distance >= _minSwipeDistance)
            {
                Vector2 swipeDirection = direction.normalized;
                
                // Determine if this is primarily a vertical swipe
                if (Mathf.Abs(swipeDirection.y) > Mathf.Abs(swipeDirection.x))
                {
                    // Calculate swipe intensity based on distance and speed
                    float swipeIntensity = direction.y * (1.0f - duration / _maxSwipeTime);
                    
                    // Trigger vertical swipe event with normalized intensity
                    OnSwipeVertical?.Invoke(swipeIntensity);
                    
                    if (_debugMode)
                    {
                        Log.Gameplay.Info($"Vertical swipe detected! Direction: {(swipeDirection.y > 0 ? "Up" : "Down")}, " +
                                          $"Intensity: {swipeIntensity}, Distance: {distance}, Duration: {duration}");
                    }
                }
            }
            else if (_debugMode && distance >= 5f) // Only log failed swipes if they had some movement
            {
                Log.Gameplay.Info($"Not a valid swipe. Distance: {distance}, Duration: {duration}");
            }
        }

        public void Dispose()
        {
            Disable();
        }
    }
}