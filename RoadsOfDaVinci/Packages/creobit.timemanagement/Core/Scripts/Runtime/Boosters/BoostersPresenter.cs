using _8floor.TimeManagement.Core.Scripts.Runtime.BaseController;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Pause;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using Creobit.Loading;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Boosters 
{

    public class BoostersPresenter : ILoadUnit, IDisposable, IReloadable {
        
        private readonly ILevelLoader _levelLoader;
        private readonly IReloadController _reloadController;
        private readonly IObjectResolver _objectResolver;
        private readonly IBoostersController _boostersController;
        private readonly IPauseController _pauseController;
        
        private readonly BoosterView _boosterViewPrefab;
        private readonly Transform _parent;

        private UnitBaseController _unitBaseController;

        private readonly CompositeDisposable _boostersPresenterDisposable = new();

        private List<BoosterView> Boosters { get; set; } = new();

        [Inject]
        private BoostersPresenter(GameplaySceneReferences gameplaySceneReferences,
            ILevelLoader levelLoader,
            IReloadController reloadController,
            IObjectResolver objectResolver,
            IBoostersController boostersController,
            IPauseController pauseController) 
        {
            _boosterViewPrefab = gameplaySceneReferences.BoosterView;
            _parent = gameplaySceneReferences.BoostersParent;
            _levelLoader = levelLoader;
            _reloadController = reloadController;
            _objectResolver = objectResolver;
            _boostersController = boostersController;
            _pauseController = pauseController;
        }

        private void LevelDataLoaded(LevelBaseSO levelBaseSO) 
        {
            AddBoosters(levelBaseSO.BoostersData, levelBaseSO.BoostersChargedOnStart);
            _pauseController.IsPaused.Skip(1).Subscribe(PauseHandler);
        }

        private void AddBoosters(BoosterDataSO[] boostersData, BoosterDataSO[] boostersChargedOnStart)
        {
            var chargedOnStart = new HashSet<BoosterDataSO>(
                boostersChargedOnStart ?? Array.Empty<BoosterDataSO>());

            foreach(BoosterDataSO boosterData in boostersData) {
                AddBooster(boosterData, chargedOnStart.Contains(boosterData));
            }
        }

        private BoosterView AddBooster(BoosterDataSO boosterData, bool chargedOnStart)
        {
            BoosterView booster = _objectResolver.Instantiate(_boosterViewPrefab, _parent);
            booster.Initialize(boosterData, chargedOnStart);
            
            Boosters.Add(booster);
            _boostersController.Boosters.Add(booster);

            return booster;
        }

        public void PauseHandler(bool IsPaused)
        {
            if(IsPaused) 
            {
                _boostersController.Pause();
            } else
            {
                _boostersController.Unpause();
            }
        }

        public UniTask Load() 
        {
            _levelLoader.LevelBaseSO
                .Skip(1)
                .Subscribe(LevelDataLoaded)
                .AddTo(_boostersPresenterDisposable);
            
            _reloadController.AddReloadableObject(this);

            return UniTask.CompletedTask;
        }

        public void Dispose() 
        {
            _boostersPresenterDisposable.Dispose();
            _reloadController.RemoveReloadableObject(this);
        }

        public UniTask Reload() 
        {
            _boostersPresenterDisposable.Clear();
            _boostersController.Reload();
            return UniTask.CompletedTask;
        }
    }

}
