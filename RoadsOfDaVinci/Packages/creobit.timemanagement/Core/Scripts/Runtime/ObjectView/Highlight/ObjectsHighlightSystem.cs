using System;
using System.Collections.Generic;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using UnityEngine;

using TMNSceneRefs = _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils.GameplaySceneReferences;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.Highlight
{
    public class ObjectsHighlightSystem : ILoadUnit, IDisposable
    {
        private readonly IObjectViewController _objectViewController;
        private readonly IComplexObjectProvider _complexObjectProvider;
        private readonly IGameplayInputSystem _inputSystem;
        private readonly IPauseController _pauseController;
        private readonly ILevelController _levelController;

        private readonly bool _enableHighlight;
        private readonly float _idleTimeToHighlight;
        private readonly Color _highlightColor;
        
        private readonly float _fadeDuration;
        private readonly int _loopsCount;
        private readonly float _maxAlpha;

        private IMovableObjectTaskManager _taskManager; 
        private float _lastActivityTime;
        private bool _isLevelStarted;
        private CancellationTokenSource _cts;
        
        private readonly List<Component> _objectsToHighlight = new();
        private readonly List<ComplexObject> _complexObjects = new();
        private readonly HashSet<StaticObjectView> _cocChildrenCache = new(); 
        private readonly Dictionary<Component, SpriteRenderer> _highlightRenderersCache = new();
        private Material _sharedHighlightMaterial;

        public ObjectsHighlightSystem(
            IObjectViewController objectViewController,
            IComplexObjectProvider complexObjectProvider,
            IGameplayInputSystem inputSystem,
            IPauseController pauseController,
            ILevelController levelController,
            TMNSceneRefs sceneReferences)
        {
            _objectViewController = objectViewController;
            _complexObjectProvider = complexObjectProvider;
            _inputSystem = inputSystem;
            _pauseController = pauseController;
            _levelController = levelController;

            var settings = sceneReferences.GameplaySettings;
            if (settings != null)
            {
                _enableHighlight = settings.EnableIdleHighlight;
                _idleTimeToHighlight = settings.IdleTimeToHighlight;
                _highlightColor = settings.HighlightColor;
                
                _fadeDuration = settings.HighlightFadeDuration;
                _loopsCount = settings.HighlightCyclesCount * 2; 
                _maxAlpha = settings.HighlightMaxAlpha;
            }
            else
            {
                _enableHighlight = true;
                _idleTimeToHighlight = 15f;
                _highlightColor = Color.white;
                _fadeDuration = 0.6f;
                _loopsCount = 6;
                _maxAlpha = 0.85f;
            }
        }

        public UniTask Load()
        {
            if (!_enableHighlight) 
                return UniTask.CompletedTask;

            _cts = new CancellationTokenSource();
            _taskManager = _objectViewController.GetMovableObjectController().GetTaskManager();

            _complexObjectProvider.CocAdded += OnCocAdded;
            _inputSystem.OnMainButtonPressed += ResetIdleTimer;

            _levelController.IsLevelStarted
                .Subscribe(started => 
                {
                    _isLevelStarted = started;
                    if (started) ResetIdleTimer();
                })
                .AddTo(_cts.Token);

            HighlightLoop(_cts.Token).Forget();

            return UniTask.CompletedTask;
        }

        private void OnCocAdded(ComplexObject coc)
        {
            if (_complexObjects.Contains(coc)) return;
            
            _complexObjects.Add(coc);
                
            var children = coc.GetComponentsInChildren<StaticObjectView>(true);
            foreach (var child in children)
            {
                _cocChildrenCache.Add(child);
            }
        }

        private void ResetIdleTimer()
        {
            _lastActivityTime = Time.time;
        }

        private async UniTaskVoid HighlightLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Yield(token);

                if (!_isLevelStarted || _pauseController.IsPaused.CurrentValue) 
                {
                    ResetIdleTimer(); 
                    continue;
                }

                if (Time.time - _lastActivityTime >= _idleTimeToHighlight)
                {
                    _lastActivityTime = Time.time; 
                    await TriggerHighlightWave(token);
                }
            }
        }

        private async UniTask TriggerHighlightWave(CancellationToken token)
        {
            var staticObjects = _objectViewController.GetStaticObjectController().StaticObjects;
    
            _objectsToHighlight.Clear();

            foreach (var staticObj in staticObjects)
            {
                if (token.IsCancellationRequested) return;

                if (staticObj.ObjectDataSO.IgnoreIdleHighlight || !staticObj.gameObject.activeInHierarchy || 
                    !staticObj.CanInteract || staticObj.IsFinalInteraction() || _taskManager.IsTaskStarted(staticObj))
                    continue;

                if (_cocChildrenCache.Contains(staticObj))
                {
                    if (!staticObj.ObjectDataSO.OverrideIgnoreIdleHighlight)
                        continue;
                }

                if (!staticObj.IsEnoughResources()) continue;

                await UniTask.Yield(); 
        
                if (await _objectViewController.CanUse(staticObj) && !_pauseController.IsPaused.CurrentValue)
                {
                    _objectsToHighlight.Add(staticObj);
                }
            }

            if (token.IsCancellationRequested || _pauseController.IsPaused.CurrentValue) return;

            foreach (var objToHighlight in _objectsToHighlight)
            {
                if (objToHighlight != null && objToHighlight.gameObject.activeInHierarchy)
                {
                    PlayHighlightEffect(objToHighlight);
                }
            }
        }

        private void PlayHighlightEffect(Component targetComponent)
        {
            var highlightRenderer = GetOrCreateHighlightRenderer(targetComponent);
            if (highlightRenderer == null) return;

            if (DOTween.IsTweening(highlightRenderer)) return;

            highlightRenderer.gameObject.SetActive(true);
            
            highlightRenderer.DOFade(_maxAlpha, _fadeDuration)
                .SetLoops(_loopsCount, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => highlightRenderer.gameObject.SetActive(false));
        }

        private SpriteRenderer GetOrCreateHighlightRenderer(Component target)
        {
            if (_highlightRenderersCache.TryGetValue(target, out var cachedRenderer) && cachedRenderer != null)
            {
                return cachedRenderer;
            }

            var mainRenderer = target.GetComponentInChildren<SpriteRenderer>();
            
            if (mainRenderer == null || mainRenderer.sprite == null)
            {
                var allRenderers = target.GetComponentsInChildren<SpriteRenderer>();
                foreach (var r in allRenderers)
                {
                    if (r.sprite != null)
                    {
                        mainRenderer = r;
                        break;
                    }
                }
            }

            if (mainRenderer == null || mainRenderer.sprite == null) return null;

            var highlightObj = new GameObject($"{target.name}_Highlight");
            highlightObj.transform.SetParent(mainRenderer.transform, false);
            highlightObj.transform.localPosition = Vector3.zero;
            highlightObj.transform.localRotation = Quaternion.identity; 
            highlightObj.transform.localScale = Vector3.one;

            var newRenderer = highlightObj.AddComponent<SpriteRenderer>();
            newRenderer.sprite = mainRenderer.sprite;
    
            newRenderer.sortingLayerID = mainRenderer.sortingLayerID;
            newRenderer.sortingOrder = mainRenderer.sortingOrder + 1; 

            if (_sharedHighlightMaterial == null)
            {
                var additiveShader = Shader.Find("Mobile/Particles/Additive");
                if (additiveShader == null) 
                {
                    additiveShader = Shader.Find("Particles/Additive");
                }

                if (additiveShader != null)
                {
                    _sharedHighlightMaterial = new Material(additiveShader);
                }
            }

            if (_sharedHighlightMaterial != null)
            {
                newRenderer.material = _sharedHighlightMaterial;
            }
    
            var startColor = _highlightColor; 
            startColor.a = 0f;
            newRenderer.color = startColor;

            highlightObj.SetActive(false);
    
            _highlightRenderersCache[target] = newRenderer;
            return newRenderer;
        }

        public void Dispose()
        {
            _inputSystem.OnMainButtonPressed -= ResetIdleTimer;
            _complexObjectProvider.CocAdded -= OnCocAdded;
            
            _cts?.Cancel();
            _cts?.Dispose();
            
            foreach (var renderer in _highlightRenderersCache.Values)
            {
                if (renderer != null) DOTween.Kill(renderer);
            }
            
            _highlightRenderersCache.Clear();
            _cocChildrenCache.Clear(); 
            _objectsToHighlight.Clear();
        }
    }
}