using System;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.InactiveObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Creobit.Bootstrap.Core.Scripts.Runtime.Fading;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;
using VContainer.Unity;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader
{
    public class LevelLoader : ILevelLoader
    {
        private static readonly int FogMainTexId = Shader.PropertyToID("_MainTex");

        private CurrentLevelSO _currentLevelSO;
        private AsyncOperationHandle<LevelBaseSO> _levelSOHandle;
        private GameObject _currentLevelObject;

        private IObjectResolver _objectResolver;
        private ILoadingController _loadingController;
        private IReloadController _reloadController;
        private GameplaySceneReferences _gameplaySceneReferences;
        private IFadeController _fadeController;

        private Camera _cachedFogCamera;
        
        private ReactiveProperty<LevelBaseSO> _currentLevelDataSO = new ();
        public ReadOnlyReactiveProperty<LevelBaseSO> LevelBaseSO => _currentLevelDataSO;
        
        public event Action BeforeLevelLoaded = delegate { };
        public event Action LevelLoaded = delegate { };

        [Inject]
        private void Construct(IObjectResolver objectResolver,
            IReloadController reloadController,
            GameplaySceneReferences gameplaySceneReferences,
			ILoadingController loadingController,
            IFadeController fadeController)
        {
            _objectResolver = objectResolver;
            _loadingController = loadingController;
            _reloadController = reloadController;
            _gameplaySceneReferences = gameplaySceneReferences;
            _fadeController = fadeController;
        }

        private void ReloadRequestedHandler()
        {
            _loadingController.AddLoadingTask(() => LoadLevel(true));
        }

        public async UniTask LoadLevel(bool reload = false)
        {
            BeforeLevelLoaded?.Invoke();
            SetFogCameraActive(false);

            await UnloadLevel();

            _levelSOHandle = _currentLevelSO.CurrentLevel.LoadAssetAsync<LevelBaseSO>();

            await _levelSOHandle.ToUniTask();

            _currentLevelDataSO.Value = _levelSOHandle.Result;

            if (reload)
            {
                _currentLevelDataSO.ForceNotify();
            }

            _currentLevelObject = _objectResolver.Instantiate(_levelSOHandle.Result.LevelPrefab);

            UpdateFogCameraForLevel(_currentLevelObject);

            await InitAllLevelObjects(_currentLevelObject);

            _currentLevelSO.WasVisited = true;

            LevelLoaded?.Invoke();
        }

        private async UniTask InitAllLevelObjects(GameObject levelObject)
        {
            foreach (var loadUnit in levelObject.GetComponentsInChildren<ILevelLoadUnit>(true))
            {
                await loadUnit.Load();
            }
        }

        private async UniTask DisposeAllLevelObjects(GameObject levelObject)
        {
            foreach (var loadUnit in levelObject.GetComponentsInChildren<ILevelLoadUnit>(true))
            {
                await loadUnit.Dispose();
            }
        }

        private async UniTask UnloadLevel()
        {
            if (!_levelSOHandle.IsValid())
            {
                return;
            }
            
            await DisposeAllLevelObjects(_currentLevelObject);

            Addressables.Release(_levelSOHandle);
                
            _currentLevelObject.AddComponent<ObjectDestroyer>();
        }

        public async void LoadNextLevel()
        {
            var isLastLevel = _currentLevelDataSO.Value.NextLevel.RuntimeKeyIsValid() == false;

            if (isLastLevel)
            {
                return;
            }

            if (_objectResolver.TryResolve<ILevelController>(out var levelController))
            {
                levelController.PrepareForNewLevel();
            }

            _currentLevelSO.CurrentLevel = _currentLevelDataSO.Value.NextLevel;

            _loadingController.AddLoadingTask(_reloadController.ReloadObjects);

            await _loadingController.ExecuteAllLoadingTasks();

            if (_currentLevelDataSO.Value != null)
            {
                _currentLevelSO.LevelNum = _currentLevelDataSO.Value.LevelNumber;
            }

            _fadeController.FadeOut().Forget();
        }
        
        public void Dispose()
        {
            if (_levelSOHandle.IsValid())
            {
                Addressables.Release(_levelSOHandle);
            }
            
            _reloadController.ReloadRequested -= ReloadRequestedHandler;
            _cachedFogCamera = null;
        }

        private void UpdateFogCameraForLevel(GameObject levelObject)
        {
            var hasFogBridge = levelObject.GetComponentInChildren<InactiveObjectsBridge>(true) != null;
            SetFogCameraActive(hasFogBridge);
        }

        private void SetFogCameraActive(bool active)
        {
            var fogTexture = _gameplaySceneReferences.FogTexture;

            if (fogTexture)
            {
                fogTexture.enabled = active;
            }

            if (!TryResolveFogCamera(out var fogCamera))
            {
                return;
            }

            fogCamera.gameObject.SetActive(active);

            if (active)
            {
                InactiveObjectsBridge.AlignFogQuad(
                    fogCamera,
                    fogTexture,
                    _gameplaySceneReferences.MainCamera);
            }
        }

        private bool TryResolveFogCamera(out Camera fogCamera)
        {
            if (_cachedFogCamera)
            {
                fogCamera = _cachedFogCamera;
                return true;
            }

            var fogMaterial = _gameplaySceneReferences.FogMaterial;
            if (!fogMaterial)
            {
                fogCamera = null;
                return false;
            }

            var fogRenderTexture = fogMaterial.GetTexture(FogMainTexId);
            if (!fogRenderTexture)
            {
                fogCamera = null;
                return false;
            }

            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (var i = 0; i < cameras.Length; i++)
            {
                var camera = cameras[i];
                if (camera.targetTexture != fogRenderTexture)
                {
                    continue;
                }

                _cachedFogCamera = camera;
                fogCamera = camera;
                return true;
            }

            fogCamera = null;
            return false;
        }

        public UniTask Load()
        {
            _currentLevelSO = _gameplaySceneReferences.CurrentLevel;

            _reloadController.ReloadRequested += ReloadRequestedHandler;

            return UniTask.CompletedTask;
        }
    }
}
