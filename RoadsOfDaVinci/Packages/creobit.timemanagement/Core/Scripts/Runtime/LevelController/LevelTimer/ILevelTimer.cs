using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Loading;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer
{
    public interface ILevelTimer : ILoadUnit, IReloadable, IDisposable
    {
        public void SetupLevelTimer(LevelTimerData levelTimerData);
        
        public void StartLevelTimer();

        public void StopLevelTimer();

        /// <summary>
        /// Приостанавливает таймер от имени источника, не сбрасывая накопленное время.
        /// Источников может быть несколько — таймер пойдёт, только когда снимут все.
        /// В отличие от <see cref="StopLevelTimer"/> интервал не отменяется.
        /// </summary>
        public void PauseLevelTimer(object source);

        /// <summary>
        /// Снимает паузу, поставленную источником.
        /// </summary>
        public void ResumeLevelTimer(object source);

        public void AffectLevelTimer(short amount);

        public LevelTimerResult EvaluateTimer();

        public uint Speed { get; set; }
    }
}