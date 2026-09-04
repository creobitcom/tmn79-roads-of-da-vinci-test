using System;
using System.Collections.Generic;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using VContainer;
using Log = Creobit.Logger.Log;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem
{
    public class GameplayInputSystem : IGameplayInputSystem
    {
        private GameplaySceneReferences _gameplaySceneReferences;

        private Camera _gameplayCamera;
        private GameplayInputActions _gameplayInputActions;
        private InputAction _primaryAction;
        private InputAction _secondaryAction;
        private InputAction _pinchAction;
        private InputAction _cursorPositionAction;
        private InputAction _pauseAction;
#if CREOBIT_DEBUG
        private InputAction _debugCloseAction;
#endif
        private Vector2 _cursorPosition;
        private bool _hoverIsActive;
        private bool _useOnCursorEnterSecondaryAction;
        private bool _hoverDelayStarted;
        private IReactToActions _hoverObjectView;
        private GameDevice _activeGameDevice;

        public bool IsActionAvailable { get; set; }

        /// <summary>
        /// Живёт только пока тикает задержка ховера. null — отложенного показа нет.
        /// Создаётся перед стартом задержки, снимается через <see cref="CancelPendingHover"/>.
        /// </summary>
        private CancellationTokenSource _hoverActionCancellation;

        public event Action OnMainButtonPressed;
        public event Action OnPauseButtonPressed;
        public event Action OnGameDeviceChanged;
        public event Action<float> OnZoom;
        public event Action<ObjectView.MovableObjects.TaskManager.IReactToActions> PrimaryActionPerformed;
        public IReactToActions CurrentPrimaryActionTarget { get; private set; }

        [Inject]
        private void Construct(GameplaySceneReferences gameplaySceneReferences)
        {
            _gameplaySceneReferences = gameplaySceneReferences;
            IsActionAvailable = true;
        }

        public UniTask Load()
        {
            _gameplayCamera = _gameplaySceneReferences.MainCamera;
            _gameplayInputActions = new GameplayInputActions();
            _primaryAction = _gameplayInputActions.TimeManagement.PrimaryAction;
            _secondaryAction = _gameplayInputActions.TimeManagement.SecondaryAction;
            _cursorPositionAction = _gameplayInputActions.TimeManagement.CursorPosition;
            _pinchAction = _gameplayInputActions.TimeManagement.Pinch;
            _pauseAction = _gameplayInputActions.TimeManagement.BackAction;
#if CREOBIT_DEBUG
            _debugCloseAction = _gameplayInputActions.TimeManagement.DebugCloseGame;
#endif
#if UNITY_STANDALONE || UNITY_EDITOR
            _useOnCursorEnterSecondaryAction = true;
#endif
            return UniTask.CompletedTask;
        }

        public void Enable()
        {
            Log.Gameplay.Info("GameplayInputSystem.Enable");
            InputSystem.onActionChange += InputSystemOnActionChange;
            _primaryAction.performed += OnPrimaryActionPerformed;
            _secondaryAction.performed += OnSecondaryActionPerformed;
            _secondaryAction.canceled += OnSecondaryActionCancelled;
            _cursorPositionAction.performed += OnCursorPositionChanged;
            _pinchAction.performed += OnPinchPerformed;
            _pauseAction.performed += OnPauseActionPerformed;
#if CREOBIT_DEBUG
            _debugCloseAction.performed += OnDebugCloseActionPerformed;
            _debugCloseAction.Enable();
#endif
            _primaryAction.Enable();
            _secondaryAction.Enable();
            _cursorPositionAction.Enable();
            _pauseAction.Enable();
            IsActionAvailable = true;
        }

        public void Disable()
        {
            Log.Gameplay.Info("GameplayInputSystem.Disable");
            InputSystem.onActionChange -= InputSystemOnActionChange;
            _primaryAction.performed -= OnPrimaryActionPerformed;
            _secondaryAction.performed -= OnSecondaryActionPerformed;
            _secondaryAction.canceled -= OnSecondaryActionCancelled;
            _cursorPositionAction.performed -= OnCursorPositionChanged;
            _pinchAction.performed -= OnPinchPerformed;
            _pauseAction.performed -= OnPauseActionPerformed;
#if CREOBIT_DEBUG
            _debugCloseAction.performed -= OnDebugCloseActionPerformed;
            _debugCloseAction.Disable();
#endif
            _primaryAction.Disable();
            _secondaryAction.Disable();
            _cursorPositionAction.Disable();
            _pauseAction.Disable();
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
            if (!Physics.Raycast(_gameplayCamera.ScreenPointToRay(_cursorPosition), out var hit))
            {
                return;
            }

            var objectView = hit.collider.GetComponent<IReactToActions>();

            if (_hoverObjectView is null || !_hoverObjectView.Equals(objectView))
            {
                // Отмена обязательна: без неё отложенный показ, заведённый для прошлого объекта,
                // доживал до конца задержки и дёргал OnSecondaryActionStart уже у НОВОГО —
                // тултип открывался второй раз и не для того, для кого таймер заводился.
                CancelPendingHover();

                if (_hoverObjectView is { IsInactiveBlocked: false })
                {
                    _hoverObjectView.OnSecondaryActionEnd();
                }

                _hoverObjectView = objectView;
            }

            if (_hoverObjectView is null || _hoverObjectView.IsInactiveBlocked)
                return;


            if (_hoverObjectView.CanReactToSecondaryAction)
            {
                if (IsActionAvailable)
                {
                    _hoverObjectView.OnSecondaryActionStart();
                }
            }
        }

        private void OnSecondaryActionCancelled(InputAction.CallbackContext obj)
        {
            _hoverIsActive = false;

            // Кнопку отпустили — но это НЕ значит, что курсор ушёл с объекта.
            //
            // PrimaryAction (Tap) и SecondaryAction (Hold) сидят на ОДНОЙ кнопке
            // (<Mouse>/leftButton, <Pointer>/press), поэтому каждый обычный клик приходит сюда
            // как «отпустили ховер». На ПК ховер ведёт курсор, а не кнопка, и раньше клик гасил
            // его целиком: тултип пропадал, объект наведения забывался, и вернуть подсказку
            // могло только движение мыши — курсор при этом всё время стоял на объекте.
            //
            // На мобиле наоборот: ховер там и есть удержание, отпустили — значит закончили.
            if (_useOnCursorEnterSecondaryAction)
            {
                return;
            }

            EndHover();
        }

        /// <summary>
        /// Курсор действительно перестал указывать на объект: ушёл с него, попал в пустоту
        /// или (на мобиле) игрок отпустил палец. Снимает отложенный показ и сообщает объекту,
        /// что ховер закончился.
        /// </summary>
        private void EndHover()
        {
            // Первым делом и безусловно: иначе отложенный показ переживает уход курсора
            // и всплывает уже после того, как игрок съехал с объекта.
            CancelPendingHover();

            if (_hoverObjectView is null || _hoverObjectView.IsInactiveBlocked)
                return;
            if (!_hoverObjectView.CanReactToSecondaryAction)
            {
                return;
            }

            _hoverObjectView.OnSecondaryActionEnd();
            _hoverObjectView = null;
        }

        private void OnPauseActionPerformed(InputAction.CallbackContext obj)
        {
            OnPauseButtonPressed?.Invoke();
        }

        private void OnCursorPositionChanged(InputAction.CallbackContext obj)
        {
            _cursorPosition = obj.ReadValue<Vector2>();

            if (!(_hoverIsActive || _useOnCursorEnterSecondaryAction))
                return;

            if (IsPointerOverUI())
            {
                // Курсор ушёл на UI. Раньше здесь был голый return: отложенный показ продолжал
                // тикать и через HoverDelay выкидывал тултип объекта поверх интерфейса,
                // причём закрыть его было нечем — OnSecondaryActionEnd приходит только когда
                // курсор вернётся в мир и съедет с объекта.
                CancelPendingHover();

                return;
            }

            if (!Physics.Raycast(_gameplayCamera.ScreenPointToRay(_cursorPosition), out var hit))
            {
                // Вот ЭТО — настоящий уход курсора: под ним пусто. Раньше здесь звался
                // OnSecondaryActionCancelled, теперь напрямую EndHover — тот же код без
                // побочного «отпустили кнопку», которое к движению мыши отношения не имеет.
#if UNITY_STANDALONE || UNITY_EDITOR
                EndHover();
#else
                CancelPendingHover();
#endif

                return;
            }

            var objectView = hit.collider.GetComponent<IReactToActions>();

            if (_hoverObjectView != objectView)
            {
                CancelPendingHover();
                _hoverObjectView?.OnSecondaryActionEnd();
                _hoverObjectView = objectView;
            }

            if (_hoverObjectView is null || _hoverObjectView.IsInactiveBlocked)
            {
                return;
            }

            if (_hoverObjectView.CanReactToSecondaryAction)
            {
                if (IsActionAvailable)
                {
                    if (!_hoverDelayStarted)
                    {
                        _hoverDelayStarted = true;
                        _hoverActionCancellation = new CancellationTokenSource();
                        CallSecondaryActionStart(_hoverObjectView, _hoverActionCancellation.Token).Forget();
                    }
                }
            }
        }

        /// <summary>
        /// Отложенный показ тултипа. Цель передаётся аргументом и сверяется на выходе: раньше
        /// метод читал поле _hoverObjectView в момент СРАБАТЫВАНИЯ таймера, и показывал тултип
        /// того объекта, на котором курсор оказался к тому времени, а не того, для которого
        /// таймер заводился.
        /// </summary>
        private async UniTask CallSecondaryActionStart(IReactToActions target, CancellationToken token)
        {
            var cancelled = await UniTask.Delay(RuntimeConstants.Delays.HoverDelay, cancellationToken: token)
                .SuppressCancellationThrow();

            if (cancelled)
            {
                // Флаг уже сброшен в CancelPendingHover.
                return;
            }

            if (!ReferenceEquals(target, _hoverObjectView))
            {
                // Курсор успел уйти на другой объект — дать ему завести свою задержку.
                _hoverDelayStarted = false;

                return;
            }

            // Флаг НЕ сбрасываем: тултип показан, и пока курсор стоит на том же объекте,
            // заводить задержку заново незачем. Раньше он сбрасывался здесь, и каждое движение
            // мыши в пределах объекта запускало новый таймер поверх старого — старый
            // CancellationTokenSource при этом терялся недиспоузенным, а OnSecondaryActionStart
            // повторялся каждые HoverDelay, пока курсор шевелится.
            target.OnSecondaryActionStart();
        }

        /// <summary>
        /// Снять отложенный показ тултипа. Одна точка на все случаи «ховер больше не в силе»:
        /// сменился объект, курсор ушёл на UI, луч не попал ни во что, вторичное действие
        /// обработали напрямую. Раньше отмена стояла только в одном из них.
        /// </summary>
        private void CancelPendingHover()
        {
            _hoverDelayStarted = false;

            if (_hoverActionCancellation == null)
            {
                return;
            }

            _hoverActionCancellation.Cancel();
            _hoverActionCancellation.Dispose();
            _hoverActionCancellation = null;
        }

        private void OnPrimaryActionPerformed(InputAction.CallbackContext obj)
        {
            OnMainButtonPressed?.Invoke();
            // Log.Gameplay.Info("OnPrimaryActionPerformed");

            if (IsPointerOverUI())
            {
                return;
            }

            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (!Physics.Raycast(_gameplayCamera.ScreenPointToRay(_cursorPosition), out var hit))
            {
                return;
            }

            var reactTo = hit.collider.GetComponent<IReactToActions>();

            if (reactTo is not null)
            {
                if (reactTo.CanReactToPrimaryAction
                    && !reactTo.IsInactiveBlocked
                    && (IsActionAvailable || reactTo.AlwaysReactToPrimaryAction))
                {
                    CurrentPrimaryActionTarget = reactTo;

                    try
                    {
                        reactTo.OnPrimaryAction();
                    }
                    finally
                    {
                        CurrentPrimaryActionTarget = null;
                    }

                    PrimaryActionPerformed?.Invoke(reactTo);
                }
            }
        }

        private bool IsPointerOverUI()
        {
            // Create pointer event data for ray casting
            PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
            {
                position = _cursorPosition
            };

            // Raycast against UI elements
            List<RaycastResult> raycastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerEventData, raycastResults);

            return raycastResults.Count > 0;
        }

        private void OnPinchPerformed(InputAction.CallbackContext obj)
        {
            if (Touch.activeTouches.Count < 2)
                return;

            var primary = Touch.activeTouches[0];
            var secondary = Touch.activeTouches[1];

            if (primary.phase == TouchPhase.Moved || secondary.phase == TouchPhase.Moved)
            {
                if (primary.history.Count < 1 || secondary.history.Count < 1)
                    return;

                var currentDistance = Vector2.Distance(primary.screenPosition, secondary.screenPosition);
                var previousDistance =
                    Vector2.Distance(primary.history[0].screenPosition, secondary.history[0].screenPosition);

                OnZoom?.Invoke(currentDistance - previousDistance);
            }
        }

        public void Dispose()
        {
            CancelPendingHover();

            Disable();
        }


#if CREOBIT_DEBUG
        private void OnDebugCloseActionPerformed(InputAction.CallbackContext obj)
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
#endif
    }
}
