using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using UnityEngine;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.Processors.Base;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Creobit.Loading;
using Creobit.Localization;
using Creobit.Logger;
using Creobit.UI;
using Cysharp.Threading.Tasks;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.View
{
    public class LevelTaskPresenter : ILoadUnit, IDisposable, IReloadable
    {
        private ILevelTasksController _levelTasksController;
        private IReloadController _reloadController;
        private GameplaySceneReferences _gameplaySceneReferences;
        private IUIController _uiController;
        private ILevelLoader _levelLoader;
        
        private LevelTaskView _levelTaskView;

        private ObjectPool<LevelTaskView> _levelTaskViewPool;
        private readonly List<LevelTaskView> _levelTaskViews = new ();

        private bool _isStartTaskViewShown;

        [Inject]
        private void Construct(ILevelTasksController levelTasksController,
            IReloadController reloadController,
            GameplaySceneReferences gameplaySceneReferences,
            IUIController uiController,
            ILevelLoader levelLoader)
        {
            _levelTasksController = levelTasksController;
            _reloadController = reloadController;
            _gameplaySceneReferences = gameplaySceneReferences;
            _uiController = uiController;
            _levelLoader = levelLoader;
        }

        private void LevelTaskAddedHandler(LevelTask levelTask)
        {
            var levelTaskView = GetLevelTaskView();
            
            levelTaskView.SetData(levelTask);
            
            levelTaskView.SetTaskName(LocalizationService.Instance.GetText(levelTask.GetTaskData().TaskName));
            
            levelTaskView.gameObject.SetActive(levelTask.GetTaskData().IsShownOnStart);
            
            _levelTaskViews.Add(levelTaskView);
        }

        private void LocalizationChangedHandler(string language)
        {
            foreach (var taskView in _levelTaskViews)
            {
                var key = taskView.GetTaskNameKey();
                
                taskView.SetTaskName(LocalizationService.Instance.GetText(key));
            }
        }

        private LevelTaskView CreateLevelTaskView()
        {
            var levelTaskView = UnityEngine.Object.Instantiate(
                _gameplaySceneReferences.LevelPveTaskView, _gameplaySceneReferences.LevelTasksParent);

            levelTaskView.gameObject.SetActive(false);
            
            return levelTaskView;
        }
        
        private LevelTaskView CreateLevelPveStartTaskView()
        {
            var levelTaskView = UnityEngine.Object.Instantiate(
                _gameplaySceneReferences.LevelTaskView, _gameplaySceneReferences.LevelTasksParent);

            levelTaskView.gameObject.SetActive(false);
            
            return levelTaskView;
        }

        private LevelTaskView GetLevelTaskView()
        {
            var levelTaskView = _levelTaskViewPool.Get();
            
            levelTaskView.gameObject.SetActive(true);

            return levelTaskView;
        }

        private void ReleaseLevelTaskView(LevelTaskView levelTaskView)
        {
            levelTaskView.gameObject.SetActive(false);
            
            levelTaskView.Clear();
            
            // _levelTaskViewPool.Release(levelTaskView);
        }

        public UniTask Load()
        {
            _levelTaskViewPool = new ObjectPool<LevelTaskView>(CreateLevelTaskView);
            
            _levelTasksController.LevelTaskAdded += LevelTaskAddedHandler;
            
            LocalizationService.Instance.OnLanguageChanged += LocalizationChangedHandler;
            
            _levelTasksController.OnLevelDataLoaded += LoadStartTaskView;

            _reloadController.AddReloadableObject(this);
            
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            foreach (var taskView in _levelTaskViews)
            {
                taskView.Dispose();
            }
            
            _levelTaskViewPool.Dispose();
            
            _levelTasksController.LevelTaskAdded -= LevelTaskAddedHandler;

            LocalizationService.Instance.OnLanguageChanged -= LocalizationChangedHandler;
            
            _levelTasksController.OnLevelDataLoaded -= LoadStartTaskView;
            
            _reloadController.RemoveReloadableObject(this);
        }

        public UniTask Reload()
        {
            ClearStartTaskView();

            if (_isStartTaskViewShown)
            {
                _uiController.HidePanel(_gameplaySceneReferences.StartTasksView, null);

                _isStartTaskViewShown = false;
            }

            return UniTask.CompletedTask;
        }

        private void ClearStartTaskView()
        {
            foreach (var taskView in _levelTaskViews)
            {
                if (taskView == null)
                {
                    continue;
                }

                UnityEngine.Object.Destroy(taskView.gameObject);
            }
            
            _levelTaskViews.Clear();
        }
        
        private void ClearStartTaskParentChildren(Transform tasksParent)
        {
            for (var i = tasksParent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(tasksParent.GetChild(i).gameObject);
            }
        }

        private async void LoadStartTaskView()
        {
            try
            {
                _uiController.ShowPanel(_gameplaySceneReferences.StartTasksView,
                    _gameplaySceneReferences.GameplayCanvasLayers[3].transform);

                _isStartTaskViewShown = true;

                var taskView = (await _uiController.GetPanel(_gameplaySceneReferences.StartTasksView))
                    .GetComponent<TasksStartView>();

                ClearStartTaskParentChildren(taskView.tasksParent);
            
                foreach (var task in _levelTasksController.LevelTasks.Values)
                {
                    var levelTaskView = UnityEngine.Object.Instantiate(
                        _gameplaySceneReferences.LevelTaskView, _gameplaySceneReferences.LevelTasksParent); //todo replace all to object pool
                
                    levelTaskView.transform.SetParent(taskView.tasksParent);
            
                    levelTaskView.SetData(task, true);
            
                    levelTaskView.SetTaskName(LocalizationService.Instance.GetText(task.GetTaskData().TaskName));
            
                    levelTaskView.gameObject.SetActive(task.GetTaskData().IsShownOnStart);
                }
                
                var levelData = _levelLoader.LevelBaseSO.CurrentValue;
                if (levelData.UseLevelNameInStartTasksView)
                {
                    taskView.levelNameText.text = LocalizationService.Instance.GetText(levelData.LevelName);
                }
                else
                {
                    taskView.levelNameText.text = string.Format(LocalizationService.Instance.GetText("goal_txt_header"),
                        levelData.LevelNumber);
                }
                
                if (taskView.levelStoryText)
                {
                    taskView.levelStoryText.text =
                        LocalizationService.Instance.GetText(_levelLoader.LevelBaseSO.CurrentValue.LevelDescription);
                            //$"StartLvl{_levelLoader.LevelBaseSO.CurrentValue.LevelNumber:D2}");
                }

                if (taskView.levelImage != null)
                {
                    taskView.levelImage.sprite = _levelLoader.LevelBaseSO.CurrentValue.LevelPveStartImage;
                }
            }
            catch (Exception e)
            {
                Log.Gameplay.Error(e);
            }
        }
    }
}