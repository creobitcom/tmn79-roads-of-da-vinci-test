using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.Actions;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.COC.UI
{
    public class CocPresenter : ILoadUnit, IDisposable, IReloadable
    {
        private GameplaySceneReferences _gameplaySceneReferences;

        private IComplexObjectProvider _complexObjectProvider;

        private IReloadController _reloadController;
        
        private ObjectPool<CocView> _cocViews;

        private ComplexObject _currentCoc;
        
        private readonly CompositeDisposable _compositeDisposable = new ();
        
        private readonly List<CocView> _activeCocList = new ();
        
        private readonly Dictionary<CocActionType, CocActionView> _actionViews = new ();

        [Inject]
        private void Construct(IComplexObjectProvider complexObjectProvider, 
            IReloadController reloadController,
            GameplaySceneReferences gameplaySceneReferences)
        {
            _gameplaySceneReferences = gameplaySceneReferences;

            _complexObjectProvider = complexObjectProvider;

            _reloadController = reloadController;
        }

        private void CocAddedHandler(ComplexObject complexObjectView)
        {
            // complexObjectView.ActionData.Skip(1) //todo rewrite for many coc options
            //     .Subscribe(AvailableActionChangedHandler)
            //     .AddTo(_compositeDisposable);
            
            // complexObjectController.TransitionDataChanged.Skip(1)
            //     .Subscribe(TransitionDataChangedHandler)
            //     .AddTo(_compositeDisposable);
        }

        private void AvailableActionChangedHandler(CocActionData actionData)
        {
            _currentCoc = actionData.ComplexObjectView;
            
            _gameplaySceneReferences.ActionViewParent.gameObject.SetActive(true);
            
            ((RectTransform)_gameplaySceneReferences.ActionViewParent).localPosition =
                UIHelper.ConvertWorldToLocalCanvasPosition(actionData.CocPosition, _gameplaySceneReferences.MainCamera,
                    _gameplaySceneReferences.GameplayCanvasLayers[0]);
            
            foreach (CocActionType cocActionType in Enum.GetValues(typeof(CocActionType)))
            {
                _actionViews[cocActionType].gameObject.SetActive(actionData.AvailableActions[cocActionType]);
            }
        }

        private void TransitionDataChangedHandler(TransitionData transitionData)
        {
            // _gameplaySceneReferences.CocTransform.gameObject.SetActive(true);
            //
            // for (var i = 0; i < transitionData.TransitionStateData.TransitionTo.Length; i++)
            // {
            //     var cocView = GetCoc();
            //     
            //     cocView.SetData(transitionData.TransitionStateData.TransitionTo[i], transitionData.TransitionStateData.TransitionTo[i].ObjectDataSO.BuildingSettings);
            //     
            //     cocView.SetTime(transitionData.TransitionTime[i]);
            //     
            //     foreach (var costData in transitionData.TransitionCosts[i].CostsData)
            //     {
            //         if (!costData.TransitionFrom.Equals(
            //                 transitionData.TransitionStateData.TransitionFrom))
            //         {
            //             continue;
            //         }
            //
            //         cocView.SetResourceData(costData.TransitionCost);
            //     }
            //     
            //     _activeCocList.Add(cocView);
            // }
        }

        private CocView CreateCoc()
        {
            var instance = Object.Instantiate(_gameplaySceneReferences.CocView, _gameplaySceneReferences.CocTransform);
            
            instance.gameObject.SetActive(false);

            instance.OnButtonClick += OnCocClick;
            
            return instance;
        }

        private async void OnCocClick(CocView cocView)
        {
            // if (!await _currentCoc.ChangeState(cocView.TransitionToRef))
            // {
            //     return;
            // }
            //
            // _gameplaySceneReferences.CocTransform.gameObject.SetActive(false);
            //
            // ClearCoc();
        }

        private void OnCocActionClick(CocActionType cocActionType)
        {
            _gameplaySceneReferences.ActionViewParent.gameObject.SetActive(false);
            
            _currentCoc.PerformAction(cocActionType);
        }

        private void ClearCoc()
        {
            foreach (var activeCocView in _activeCocList)
            {
                ReleaseCoc(activeCocView);
            }

            _activeCocList.Clear();
        }

        private CocView GetCoc()
        {
            var coc = _cocViews.Get();
            
            coc.gameObject.SetActive(true);

            return coc;
        }

        private void ReleaseCoc(CocView cocView)
        {
            cocView.gameObject.SetActive(false);
            
            _cocViews.Release(cocView);
        }

        public UniTask Load()
        {
            _cocViews = new ObjectPool<CocView>(CreateCoc);
            
            _complexObjectProvider.CocAdded += CocAddedHandler;
            
            _reloadController.AddReloadableObject(this);

            foreach(var cocActionView in _gameplaySceneReferences.ActionViews) {
                cocActionView.OnButtonClick += OnCocActionClick;

                _actionViews.Add(cocActionView.ActionType, cocActionView);
            }

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            foreach (var coc in _activeCocList)
            {
                coc.OnButtonClick -= OnCocClick;
            }

            foreach(var cocActionView in _gameplaySceneReferences.ActionViews) {
                cocActionView.OnButtonClick -= OnCocActionClick;
            }

            _complexObjectProvider.CocAdded -= CocAddedHandler;
            
            _reloadController.RemoveReloadableObject(this);
            
            _compositeDisposable.Dispose();
        }

        public UniTask Reload()
        {
            _gameplaySceneReferences.CocTransform.gameObject.SetActive(false);
            
            _compositeDisposable.Clear();
            
            ClearCoc();
            
            return UniTask.CompletedTask;
        }
    }
}