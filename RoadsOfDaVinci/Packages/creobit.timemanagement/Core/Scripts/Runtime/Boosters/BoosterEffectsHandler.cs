using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Creobit.Logger;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Boosters
{
    public class BoosterEffectsHandler
    {
        private ILevelTimer _levelTimer;

        private uint _savedTimerSpeed;

        public BoosterEffectsHandler(ILevelTimer levelTimer) 
        {
            _levelTimer = levelTimer;
        }

        private void SetUnitWalkSpeed(BoosterDataSO boosterData, MovableObjectView unit, bool reset)
        {
            var speed = Arithmetic.CountMode(
                unit.CurrentSpeed, boosterData.UnitsWalkSpeed.Value, boosterData.UnitsWalkSpeed.Mode, reset);

            if (speed == 0)
            {
                Log.Gameplay.Error("Бустеры скорости не поддерживают значения равные нулю");
                return;
            }

            if (boosterData.UnitsWalkSpeed.Value > 0)
            {
                unit.EnableSpeedBoosterEffect(!reset);
            }

            unit.SetSpeed(speed);
        }

        private void SetUnitInteractionSpeed(BoosterDataSO boosterData, MovableObjectView unit, bool reset)
        {
            var interactionSpeed = Arithmetic.CountMode(
                unit.InteractionSpeed, boosterData.UnitsInteractionSpeed.Value, boosterData.UnitsInteractionSpeed.Mode, reset);

            if (interactionSpeed == 0)
            {
                Log.Gameplay.Error("Бустеры скорости взаимодействия не поддерживают значения равные нулю");
                return;
            }

            unit.SetBoosterInteractionSpeed(interactionSpeed);
        }

        private void SetTimerSpeed(BoosterDataSO boosterData, bool reset)
        {
            var floatSpeed = Arithmetic.CountMode(
                _levelTimer.Speed, boosterData.TimerSpeed.Value, boosterData.TimerSpeed.Mode, reset);

            var speed = floatSpeed > 0 ? (uint) floatSpeed : 0;

            if(speed == 0)
            {
                if(reset)
                {
                    _levelTimer.Speed = _savedTimerSpeed;
                    return;
                }

                if(_levelTimer.Speed != 0)
                {
                    _savedTimerSpeed = _levelTimer.Speed;
                    _levelTimer.Speed = speed;
                }

                return;
            }

            _levelTimer.Speed = speed;
        }

        public async UniTask SetEffects(BoosterDataSO boosterData, MovableObjectView unit, bool reset)
        {
            SetTimerSpeed(boosterData, reset);

            await UniTask.WaitUntil(() => unit.WasLoaded);
            SetUnitInteractionSpeed(boosterData, unit, reset);
            SetUnitWalkSpeed(boosterData, unit, reset);
        }

        public void GiveCurrentEffects(List<BoosterView> currentBoosters, MovableObjectView unit)
        {
            foreach (var booster in currentBoosters)
            {
                if (booster.IsActive)
                {
                    SetEffects(booster.BoosterData, unit, false).Forget();
                }
            }
        }
    }
}
