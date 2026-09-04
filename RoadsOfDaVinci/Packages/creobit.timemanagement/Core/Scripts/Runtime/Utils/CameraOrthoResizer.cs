using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils
{
    public class CameraOrthoResizer : MonoBehaviour
    {
        [Title("Background Configuration")]
        [InfoBox("Settings that define how the camera adjusts to the background")]
        
        [PropertyOrder(1)]
        [LabelText("Width (px)")]
        [InfoBox("Background image width in pixels")]
        [SerializeField] private float _backgroundWidth = 1920f;

        [PropertyOrder(2)]
        [LabelText("Height (px)")]
        [InfoBox("Background image height in pixels")]
        [SerializeField] private float _backgroundHeight = 1080f;

        [PropertyOrder(3)]
        [LabelText("Pixels Per Unit")]
        [InfoBox("Pixels per Unity unit for the background sprite")]
        [SerializeField] private float _pixelsPerUnit = 100f;

        [SerializeField] private CinemachineCamera _virtualCamera;


        private Camera _camera;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private float _backgroundWorldWidth;
        private float _backgroundWorldHeight;
        private float _backgroundAspectRatio;
        private float _designAspectRatio;
        private float _cameraAspectRatio = 1;
        private float _lastCameraAspectRatio = 1;

        [Button]
        private void Awake()
        {
            _backgroundWorldWidth = _backgroundWidth / _pixelsPerUnit;
            _backgroundWorldHeight = _backgroundHeight / _pixelsPerUnit;
            _backgroundAspectRatio = _backgroundHeight / _backgroundWidth;
            _designAspectRatio = _backgroundWidth / _backgroundHeight;
        }

        private void Start()
        {
            _camera = Camera.main;

            if (_camera == null)
            {
                Debug.LogWarning("Main camera not found. CameraOrthoResizer will not function.");
                enabled = false;
                return;
            }

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            AdjustCameraSize();
        }

        [Inject]
        private void Construct(IGameplayInputSystem gameplayInputSystem)
        {
            gameplayInputSystem.OnZoom += Zoom;
        }

        private void Update()
        {
            if (!HasScreenResolutionChanged())
            {
                return;
            }

            AdjustCameraSize();
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
        }

        private bool HasScreenResolutionChanged()
        {
            return Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight 
                                                    || !Mathf.Approximately(_cameraAspectRatio, _lastCameraAspectRatio);
        }

        private void AdjustCameraSize()
        {
            var screenAspectRatio = (float)Screen.width / Screen.height;

            if (screenAspectRatio > _designAspectRatio)
            {
                ApplyPillarboxing();
            }
            else if (screenAspectRatio >= _backgroundAspectRatio)
            {
                ApplyLetterboxing(screenAspectRatio);
            }
            else
            {
                ApplyPillarboxing();
            }

            _lastCameraAspectRatio = _cameraAspectRatio;
        }

        private void ApplyPillarboxing()
        {
            ApplyOrthoSize(_backgroundWorldHeight / 2f * _cameraAspectRatio);
        }

        private void ApplyLetterboxing(float screenAspectRatio)
        {
            ApplyOrthoSize(_backgroundWorldWidth / (2f * screenAspectRatio) * _cameraAspectRatio);
        }

        private void ApplyOrthoSize(float orthographicSize)
        {
            if (_virtualCamera)
            {
                _virtualCamera.Lens.OrthographicSize = orthographicSize;
                return;
            }

            _camera.orthographicSize = orthographicSize;
        }

        private void Zoom(float distance)
        {
            _cameraAspectRatio += distance * 0.01f;;
        }
    }
}
