using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors.Base;
using R3;
using TMPro;
using UltEvents;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.View
{
    public class LevelTaskView : MonoBehaviour, IDisposable
    {
        [SerializeField] 
        private TMP_Text _taskNameText;
        
        [SerializeField] 
        private TMP_Text _taskAmountText;

        [SerializeField] private string _amountPrefix = string.Empty;
        [SerializeField] private string _amountSufix = string.Empty;
        
        [SerializeField] private Image _taskIcon;

        [FormerlySerializedAs("onTaskCompleted")] [SerializeField]
        private UltEvent _onTaskCompleted; 
        
        [FormerlySerializedAs("onClear")] [SerializeField]
        private UltEvent _onClear; 

        private readonly CompositeDisposable _compositeDisposable = new ();

        private LevelTask _levelTask;

        private LevelTaskData _levelTaskData;

        private string _currentProgress;
        private string _currentTaskName;
        private bool _isLevelPveStart;

        public void SetTaskName(string taskName)
        {
            _currentTaskName = taskName;
            
            ProgressChanged(_currentProgress);
        }

        public string GetTaskNameKey()
        {
            return _levelTaskData.TaskName;
        }

        public void SetAmountToMax()
        {
            _taskAmountText.text = _amountPrefix+_levelTask.GetTaskData().AmountToCompleteTask.ToString()+_amountSufix;
        }

        public void SetData(LevelTask levelTask, bool levelPveStart = false)
        {
            _levelTaskData = levelTask.GetTaskData();

            _levelTask = levelTask;
            
            if (_taskIcon != null && _levelTaskData.TaskIcon != null)
            {
                _taskIcon.sprite = _levelTaskData.TaskIcon;
            }

            _levelTask.TaskStatusChanged += TaskStatusChanged;
            
            _taskNameText.fontStyle &= ~FontStyles.Strikethrough;

            _isLevelPveStart = levelPveStart;
            
            _levelTask.ProgressChanged
                .Subscribe(ProgressChanged)
                .AddTo(_compositeDisposable);

            _levelTask.IsShown
                .Subscribe(TaskVisibilityChanged)
                .AddTo(_compositeDisposable);
        }

        public void TryRebuildLayout()
        {
            var layout = GetComponentInParent<LayoutGroup>();
            if (layout != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(layout.transform as RectTransform);
            }
        }

        private void TaskStatusChanged(LevelTaskData levelTaskData)
        {
            if (levelTaskData.TaskStatus == LevelTaskStatus.Done)
            {
                TaskCompleted();
            }
        }

        private void TaskVisibilityChanged(bool visibility)
        {
            gameObject.SetActive(visibility);
        }

        private void TaskCompleted()
        {
            _onTaskCompleted?.Invoke();
            
            _levelTask.TaskStatusChanged -= TaskStatusChanged;
        }

        private void ProgressChanged(string progress)
        {
            _currentProgress = progress;
            
            _taskNameText.text = $"{_currentTaskName}";
            _taskAmountText.text = $"{_amountPrefix+_currentProgress+_amountSufix}";
            
            if (_isLevelPveStart)
            {
                _taskAmountText.text = _amountPrefix+_levelTaskData.AmountToCompleteTask.ToString()+_amountSufix;
            }
            
            if (_taskAmountText.text == string.Empty)
                _taskAmountText.text = _amountPrefix+ "1"+_amountSufix;

        }

        public void Clear()
        {
            // TaskCompleted();
            _onClear?.Invoke();
            
            _compositeDisposable.Clear();
        }

        public void Dispose()
        {
            //TaskCompleted();
            
            _compositeDisposable.Dispose();
        }
    }
}