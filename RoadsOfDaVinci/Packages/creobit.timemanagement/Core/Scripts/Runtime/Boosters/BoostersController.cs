using _8floor.TimeManagement.Core.Scripts.Runtime.BaseController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Boosters 
{

    public class BoostersController : IBoostersController
    {
        private readonly ILevelTimer _levelTimer;
        private readonly ILevelController _levelController;
        private readonly IObjectViewController _objectViewController;
        private UnitBaseController _unitBaseController;
        private MovableObjectController _movableObjectController;

        public event Action<BoosterDataSO> OnBoosterUsedEvent;
        
        private readonly List<BoosterView> _currentBoostersView = new();
        private BoosterEffectsHandler _effectsHandler;
        private CancellationTokenSource _cancellationTokenSource;
        private IDisposable _isLevelStarted;
        private bool _isPaused;
        private bool _reload;
        
        public List<BoosterView> Boosters { get; } = new();

        [Inject]
        public BoostersController(
            IObjectViewController objectViewController,
            ILevelTimer levelTimer,
            ILevelController levelController) 
        {
            _levelTimer = levelTimer;
            _levelController = levelController;
            _objectViewController = objectViewController;
        }

        public UniTask Load()
        {
            _unitBaseController = _objectViewController.GetUnitBaseController();
            _movableObjectController = _objectViewController.GetMovableObjectController();
            
            _effectsHandler = new BoosterEffectsHandler(_levelTimer);
            _movableObjectController.MovableUnitAdded += GiveCurrentEffects;

            _isLevelStarted = _levelController.IsLevelStarted.Subscribe(OnLevelStarted);

            Pause();
            
            return UniTask.CompletedTask;
        }

        public void Reload()
        {
            _reload = true;
            
            _currentBoostersView.Clear();
            
            foreach (var booster in Boosters)
            {
                booster.Initialize(booster.BoosterData, booster.ChargedOnStart);
                booster.OnBoosterReloaded.InvokeSafe();
            }
        }

        public void Dispose()
        {
            if (_movableObjectController != null)
                _movableObjectController.MovableUnitAdded -= GiveCurrentEffects;
            if (_isLevelStarted != null)
                _isLevelStarted.Dispose();
        }

        public async UniTask UseBooster(BoosterView boosterView) 
        {
            if (!boosterView.CanBeUsed) return;

            var boosterData = boosterView.BoosterData;

            var relevantUnits = GetRelevantUnits(boosterData);
            
            if (relevantUnits.Count == 0) return;

            _currentBoostersView.Add(boosterView);

            SetActiveState(boosterView);
            SetEffects(boosterData, relevantUnits, true);

            OnBoosterUsedEvent?.Invoke(boosterData);
            
            await UsageInterval(boosterView);

            relevantUnits = GetRelevantUnits(boosterData);

            SetChargingState(boosterView);
            SetEffects(boosterData, relevantUnits, false);

            await ChargingInterval(boosterView);

            SetDefaultState(boosterView);
            
            _currentBoostersView.Remove(boosterView);
        }

        private List<MovableObjectView> GetRelevantUnits(BoosterDataSO boosterData) 
        {
            var relevantBases = _unitBaseController.GetRelevantBases(boosterData.UnitsTypeTags, boosterData.TypeTagsContainsMode);

            return _movableObjectController.GetRelevantUnits(relevantBases.relevantBases, boosterData.TypeTagsContainsMode, boosterData.UnitsTypeTags);
        }

        private void SetActiveState(BoosterView boosterView)
        {
            boosterView.CanBeUsed = false;

            boosterView.IsActive = true;
            
            boosterView.PlayAudio(boosterView.BoosterData.UsageSound);
            boosterView.OnBoosterUsed.InvokeSafe();
        }

        private void SetChargingState(BoosterView boosterView)
        {
            boosterView.IsActive = false;

            if (_reload) return;
            
            boosterView.OnBoosterEnded.InvokeSafe();
        }

        private void SetDefaultState(BoosterView boosterView)
        {
            if (_reload) return;
            
            boosterView.CanBeUsed = true;
            
            boosterView.OnBoosterCharged.InvokeSafe();
        }

        private void SetEffects(BoosterDataSO boosterData, List<MovableObjectView> relevantUnits, bool enable)
        {
            foreach (var unit in relevantUnits)
            {
                _effectsHandler.SetEffects(boosterData, unit, !enable).Forget();
            }
        }

        private async UniTask UsageInterval(BoosterView boosterView) 
        {
            try 
            {
                _cancellationTokenSource = new CancellationTokenSource();

                var charge = boosterView.BoosterData.UsageDurationInSeconds;

                while (boosterView.Charge > 0f)
                {
                    if(_reload)
                    {
                        boosterView.Charge = 0f;
                        boosterView.WhileBoosterActive.InvokeSafe();
                        return;
                    }

                    charge -= 0.015f;
                    boosterView.Charge = charge / boosterView.BoosterData.UsageDurationInSeconds;
                    await IntervalUpdate(boosterView);
                    boosterView.WhileBoosterActive.InvokeSafe();
                }

                if (!_reload) boosterView.Charge = 0f;
            } 
            catch (OperationCanceledException) 
            {
                _cancellationTokenSource = new CancellationTokenSource();
            }
        }

        private async UniTask ChargingInterval(BoosterView boosterView) 
        {
            try 
            {
                _cancellationTokenSource = new CancellationTokenSource();

                var charge = boosterView.Charge * boosterView.BoosterData.ChargeDurationInSeconds;

                while (boosterView.Charge < 1f)
                {
                    if (_reload)
                    {
                        boosterView.Charge = 0f;
                        boosterView.WhileBoosterCharging.InvokeSafe();
                        return;
                    }

                    await IntervalUpdate(boosterView);

                    charge += 0.015f;
                    boosterView.Charge = charge / boosterView.BoosterData.ChargeDurationInSeconds;
                    boosterView.WhileBoosterCharging.InvokeSafe();
                }

                if (!_reload) boosterView.Charge = 1f;
            } 
            catch (OperationCanceledException) 
            {
                _cancellationTokenSource = new CancellationTokenSource();
            }
        }

        private async UniTask IntervalUpdate(BoosterView boosterView)
        {
            if (_isPaused)
            {
                await UniTask.WaitWhile(() => _isPaused);
            }

            await UniTask.Delay(10, cancellationToken: _cancellationTokenSource.Token);
        }

        private void GiveCurrentEffects(MovableObjectView unit)
        {
            _effectsHandler.GiveCurrentEffects(_currentBoostersView, unit);
        }

        public async UniTask ChargeBooster(BoosterView boosterView)
        {
            await ChargingInterval(boosterView);
            SetDefaultState(boosterView);
        }

        private void OnLevelStarted(bool started)
        {
            if (!started) return;
            
            Unpause();

            if (!_reload) return;
            
            _reload = false;
            foreach (var booster in Boosters)
            {
                booster.OnBoosterReloaded.InvokeSafe();

                booster.ResetCharge();
            }
        }

        public void Pause()
        {
            _isPaused = true;
        }

        public void Unpause()
        {
            _isPaused = false;
        }
    }

}
