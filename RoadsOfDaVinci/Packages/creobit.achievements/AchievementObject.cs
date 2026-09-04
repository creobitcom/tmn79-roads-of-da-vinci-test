using System;
using R3;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Achievements
{
    public abstract class AchievementObject : IDisposable
    {
        private readonly Observable<Unit> _achievementCompleted;
        public AchievementStatus AchievementStatus;
        private event Action AchievementCompleted = delegate { };

        protected readonly CompositeDisposable CompositeDisposable = new();

        protected AchievementData AchievementData;
        protected ushort CurrentAmount;

        protected AchievementObject(AchievementData achievementData)
        {
            AchievementData = achievementData;

            _achievementCompleted = Observable
                .FromEvent(a => AchievementCompleted += a,
                    a => AchievementCompleted -= a);
        }

        public void AddAchievementCompleted(Action achievementCompletedEvent)
        {
            _achievementCompleted
                .Subscribe(_ => achievementCompletedEvent?.Invoke())
                .AddTo(CompositeDisposable);
        }

        public AchievementData GetAchievementData()
        {
            return AchievementData;
        }

        public void ChangeProgress(short amount)
        {
            CurrentAmount += (ushort)Mathf.Max(0, amount);

            CheckProgress();
        }

        protected void CheckProgress()
        {
            if (CurrentAmount < AchievementData.AmountToCompleteAchievement 
                || AchievementStatus == AchievementStatus.Completed)
            {
                return;
            }

            ChangeAchievementStatus(AchievementStatus.Completed);
        }

        protected void ChangeAchievementStatus(AchievementStatus status)
        {
            AchievementStatus = status;
            
            if (status == AchievementStatus.Completed)
            {
                AchievementCompleted?.Invoke();
            }
        }

        public virtual void Setup()
        {
            
        }

    public virtual void Dispose()
        {
            CompositeDisposable.Dispose();
        }
    }
}