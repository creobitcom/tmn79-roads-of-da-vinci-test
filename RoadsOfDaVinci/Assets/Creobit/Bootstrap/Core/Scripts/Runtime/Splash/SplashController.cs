using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Creobit.AddressablesController;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Object = UnityEngine.Object;
using UnityEngine.Networking;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Splash
{
    public class SplashController : ISplashController
    {
        private BootstrapPrefabReferences _bootstrapPrefabReferences;
        private IAddressablesController _addressablesController;
        
        private GameObject _splashCanvas;
        private Image _splashImage;
        private Button _splashButton;

        private bool _canSkipCurrentSplash;
        
        private readonly string _splashDataJsonPath = Application.streamingAssetsPath + "/Splash/splashes.txt";
        private SplashDataJson _splashDataJson;

        [Inject]
        private void Construct(BootstrapPrefabReferences bootstrapPrefabReferences,
            IAddressablesController addressablesController)
        {
            _bootstrapPrefabReferences = bootstrapPrefabReferences;
            _addressablesController = addressablesController;
        }
        
        public async UniTask Load()
        {
            await LoadSplashCanvas();
        }

        private async UniTask LoadSplashCanvas()
        {
            var splashCanvasPrefab = await _addressablesController.LoadAssetByReferenceAsync<GameObject>
                (_bootstrapPrefabReferences.SplashUICanvas);
            
            _splashCanvas = Object.Instantiate(splashCanvasPrefab);
            _splashCanvas.gameObject.SetActive(false);
            _splashImage = _splashCanvas.GetComponentInChildren<Image>();
            _splashButton = _splashCanvas.GetComponentInChildren<Button>();
            _splashImage.color = Color.clear;
        }

        public async UniTask Show()
        {
            await DeserializeSplashDataJson();
            if (_splashDataJson == null || _splashDataJson.SplashDataList == null || _splashDataJson.SplashDataList.Count == 0)
            {
                Log.Bootstrap.Warning("No splash screens to show ");
                // Log.Bootstrap.Info( (_splashDataJson == null).ToString());
                // Log.Bootstrap.Info((_splashDataJson != null && (_splashDataJson.SplashDataList == null)).ToString());
                // Log.Bootstrap.Info((_splashDataJson != null && _splashDataJson.SplashDataList != null && (_splashDataJson.SplashDataList.Count == 0)).ToString());
                return;
            }

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            _splashButton.onClick.AddListener(() =>
            {
                if (!_canSkipCurrentSplash)
                    return;

                cancellationTokenSource.Cancel();
            });

            foreach (var splash in _splashDataJson.SplashDataList)
            {
                cancellationTokenSource = new CancellationTokenSource();
                const float fadePercentage = 0.25f;
                var fadeTime = splash.SplashImageTime * fadePercentage;
                try
                {
                    var uri = $"file://{Application.streamingAssetsPath}/Splash/{splash.SplashImageName}";
                    WWW localFile;
#if UNITY_ANDROID && !UNITY_EDITOR
		            string androidPath = "jar:file://" + Application.dataPath + "!/assets/Splash/" + splash.SplashImageName;
		            localFile = new WWW(androidPath);
#else
                    localFile = new WWW(uri);
#endif
                    await localFile;
                    Texture texture = localFile.texture;
                    var sprite = Sprite.Create(texture as Texture2D, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    
                    if (sprite == null)
                    {
                        Log.Bootstrap.Error($"Failed to prepare sprite for: {splash.SplashImageName}");
                        continue;
                    }

                    _splashImage.sprite = sprite;
                    _splashImage.preserveAspect = true;
                    _splashCanvas.gameObject.SetActive(true);

                    _splashImage.DOColor(Color.white, fadeTime);
                    await UniTask.Delay(TimeSpan.FromSeconds(fadeTime));

                    _canSkipCurrentSplash = splash.CanSkip;

                    await UniTask.Delay(TimeSpan.FromSeconds(splash.SplashImageTime), cancellationToken: cancellationTokenSource.Token);

                    _splashImage.DOColor(Color.clear, fadeTime);
                    await UniTask.Delay(TimeSpan.FromSeconds(fadeTime), cancellationToken: cancellationTokenSource.Token);
                    
                    // Cleanup
                    if (sprite != null)
                    {
                        if (sprite.texture != null)
                            Object.Destroy(sprite.texture);
                        Object.Destroy(sprite);
                    }
                }
                catch (OperationCanceledException)
                {
                    await ResetSplashImage(fadeTime, cancellationTokenSource);
                }
                catch (Exception ex)
                {
                    Log.Bootstrap.Error($"Error showing splash {splash.SplashImageName}: {ex}");
                    await ResetSplashImage(fadeTime, cancellationTokenSource);
                }
            }

            _splashButton.onClick.RemoveAllListeners();
        }

        private async UniTask ResetSplashImage(float fadeTime, CancellationTokenSource cancellationTokenSource)
        {
            _splashImage.DOColor(Color.clear, fadeTime);
            await UniTask.Delay(TimeSpan.FromSeconds(fadeTime));
        }

        private async Task DeserializeSplashDataJson()
        {
            try
            {
                string jsonContent = "";
#if UNITY_ANDROID && !UNITY_EDITOR
                var configPath = "jar:file://" + Application.dataPath + "!/assets/Splash/" + "splashes.txt";
                WWW www = new WWW (configPath);
                await www;
                jsonContent = www.text;
#else
                jsonContent = await File.ReadAllTextAsync(_splashDataJsonPath);
#endif
                _splashDataJson = JsonConvert.DeserializeObject<SplashDataJson>(jsonContent);
            }
            catch (Exception ex)
            {
                Log.Bootstrap.Error($"Failed to deserialize splash data: {ex}");
                _splashDataJson = new SplashDataJson(new List<SplashData>());
            }
        }

        public void Dispose()
        {
            if (_splashButton != null)
            {
                _splashButton.onClick.RemoveAllListeners();
            }

            _splashImage = null;
            _splashButton = null;
            _addressablesController.UnloadAssetReference(_bootstrapPrefabReferences.SplashUICanvas);
        }
    }

    [Serializable, Preserve]
    sealed class SplashData
    {
        public string SplashImageName;
        public float SplashImageTime;
        public bool CanSkip;

        [Preserve]
        public SplashData(string splashImageName, float splashImageTime, bool canSkip)
        {
            SplashImageName = splashImageName;
            SplashImageTime = splashImageTime;
            CanSkip = canSkip;
        }
    }
    
    [Serializable, Preserve]
    sealed class SplashDataJson
    {
        public List<SplashData> SplashDataList = new List<SplashData>();

        [Preserve]
        public SplashDataJson(List<SplashData> splashDataList)
        {
            SplashDataList = splashDataList;
        }
    }
}