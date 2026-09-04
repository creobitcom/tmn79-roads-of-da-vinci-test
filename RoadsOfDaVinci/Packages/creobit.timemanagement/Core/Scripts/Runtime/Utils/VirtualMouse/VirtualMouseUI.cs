using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using Creobit.Logger;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils.VirtualMouse
{
    public class VirtualMouseUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvasRectTransform;
        [SerializeField] private RectTransform _mouseVisualRectTransform;
        private VirtualMouseInput _virtualMouseInput;
        private IGameplayInputSystem _gameplayInputSystem;
        private bool _isActive;

        private void Awake()
        {
            _virtualMouseInput = GetComponent<VirtualMouseInput>();
        }

        [Inject]
        private void Construct(IGameplayInputSystem gameplayInputSystem)
        {
            _gameplayInputSystem = gameplayInputSystem;
            Init();
        }

        private void Init()
        {
            _gameplayInputSystem.OnGameDeviceChanged += GameplayInputSystemOnOnGameDeviceChanged;
            _isActive = true;
        }

        private void OnDestroy()
        {
            _gameplayInputSystem.OnGameDeviceChanged -= GameplayInputSystemOnOnGameDeviceChanged;
        }

        private void GameplayInputSystemOnOnGameDeviceChanged()
        {
            Log.Gameplay.Info($"GameplayInputSystemOnOnGameDeviceChanged = {_gameplayInputSystem.GetActiveGameDevice()}");
            if (_gameplayInputSystem.GetActiveGameDevice() == GameDevice.Gamepad)
            {
                ResetMouseToCenter();
                Show();
            }
            else
            {
                Hide();
            }
        }

        private void ResetMouseToCenter()
        {
            _mouseVisualRectTransform.anchoredPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
        }

        private void Show()
        {
            gameObject.SetActive(true);
            _mouseVisualRectTransform.gameObject.SetActive(true);
        }
        
        private void Hide()
        {
            gameObject.SetActive(false);
            _mouseVisualRectTransform.gameObject.SetActive(false);
        }
        private void Update()
        {
            if (!_isActive)
                return;
            var scaleFactor = Vector3.one * (1f / _canvasRectTransform.localScale.x);
            
            transform.localScale = scaleFactor;
            _mouseVisualRectTransform.localScale = scaleFactor;
            
            transform.SetAsLastSibling();
        }

        private void LateUpdate()
        {
            if (!_isActive)
                return;
            Vector2 virtualMousePosition = _virtualMouseInput.virtualMouse.position.value;
            virtualMousePosition.x = Mathf.Clamp(virtualMousePosition.x, 0f, Screen.width);
            virtualMousePosition.y = Mathf.Clamp(virtualMousePosition.y, 0f, Screen.height);
            InputState.Change(_virtualMouseInput.virtualMouse.position, virtualMousePosition);
        }
    }
}
