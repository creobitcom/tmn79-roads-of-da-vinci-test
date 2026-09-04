using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Data;
using R3;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors.Base
{
    public abstract class LevelTask : IDisposable
    {
        private readonly Observable<Unit> _taskCompleted;

        private readonly ReactiveProperty<bool> _isShown = new();
        private readonly ReactiveProperty<string> _progressChanges = new();

        private event Action TaskCompleted = delegate { };
        public event Action<LevelTaskData> TaskStatusChanged = delegate { };

        public ReadOnlyReactiveProperty<string> ProgressChanged => _progressChanges;
        public ReadOnlyReactiveProperty<bool> IsShown => _isShown;

        protected readonly CompositeDisposable CompositeDisposable = new();

        protected LevelTaskData LevelTaskData;

        public bool _showOnlyAmount;
        
        protected LevelTask(LevelTaskData levelTaskData, bool showOnlyAmount)
        {
            LevelTaskData = levelTaskData;

            TaskStatusChanged += TaskStatusChangedHandler;
            _showOnlyAmount = showOnlyAmount;
            _taskCompleted = Observable
                .FromEvent(a => TaskCompleted += a,
                    a => TaskCompleted -= a);
        }

        private void TaskStatusChangedHandler(LevelTaskData levelTaskData)
        {
            if (levelTaskData.TaskStatus == LevelTaskStatus.Done)
            {
                TaskCompleted?.Invoke();
            }
        }

        public void ChangeTaskStatus(LevelTaskStatus levelTaskStatus)
        {
            LevelTaskData.TaskStatus = levelTaskStatus;

            TaskStatusChanged?.Invoke(LevelTaskData);
        }

        public void ChangeTaskVisibility(bool visible)
        {
            _isShown.Value = visible;
        }

        public void AddTaskCompleted(Action taskCompletedEvent)
        {
            _taskCompleted
                .Subscribe(_ => taskCompletedEvent?.Invoke())
                .AddTo(CompositeDisposable);
        }

        public LevelTaskData GetTaskData()
        {
            return LevelTaskData;
        }

        public abstract void ChangeProgress(short amount);

        public virtual void Setup()
        {
            _progressChanges.Value = GetProgressDisplay(0);
        }

        public virtual void Dispose()
        {
            CompositeDisposable.Dispose();

            TaskStatusChanged -= TaskStatusChangedHandler;
        }

        protected void CheckProgress(ushort currentAmount)
        {
            if (LevelTaskData.TaskStatus == LevelTaskStatus.Done)
            {
                return;
            }

            _progressChanges.Value = GetProgressDisplay(currentAmount);

            if (currentAmount < LevelTaskData.AmountToCompleteTask)
            {
                return;
            }

            ChangeTaskStatus(LevelTaskStatus.Done);
        }

        protected virtual string GetProgressDisplay(ushort currentAmount)
        {
            return _showOnlyAmount ? $"{Mathf.Max(0, LevelTaskData.AmountToCompleteTask - currentAmount)}" :
                $"{(ushort)Mathf.Min(currentAmount, LevelTaskData.AmountToCompleteTask)}/{LevelTaskData.AmountToCompleteTask}";
        }

        private string ToHEX(Color color)
        {
            Color32 c32 = color;
            var htmlColor = "#" + c32.r.ToString("X2") + c32.g.ToString("X2") + c32.b.ToString("X2");
            return htmlColor;
        }
    }
}