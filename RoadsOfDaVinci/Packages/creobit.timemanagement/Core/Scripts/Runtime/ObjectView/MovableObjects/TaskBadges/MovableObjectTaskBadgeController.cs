using System.Collections.Generic;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskBadges
{
    public class MovableObjectTaskBadgeController : IMovableObjectTaskBadgeController
    {
        private IObjectResolver _objectResolver;

        private IReloadController _reloadController;
        
        private GameplaySceneReferences _sceneReferences;
        
        private ObjectPool<TaskBadge> _badgePool;
        
        private List<TaskBadgeWrapper> _badgeSprites;
        
        private readonly List<TaskBadge> _activeBadges = new ();
        
        [Inject]
        private void Construct(IObjectResolver objectResolver,
            IReloadController reloadController,
            GameplaySceneReferences gameplaySceneReferences)
        {
            _objectResolver = objectResolver;

            _reloadController = reloadController;

            _sceneReferences = gameplaySceneReferences;
        }
        
        private TaskBadge CreateTaskBadge()
        {
            var instance = _objectResolver.Instantiate(_sceneReferences.TaskBadge, 
                _sceneReferences.GameplayCanvasLayers[0].transform);
            
            instance.gameObject.SetActive(false);

            return new TaskBadge(instance.badgeImage, instance.backImage, instance.badgeText, instance.gameObject);
        }

        private void TryGetBadge(ushort taskId, out TaskBadge taskBadge)
        {
            HandleExistingBadge(taskId);
            
            taskBadge = GetTaskBadge();
        }

        private void HandleExistingBadge(ushort taskId)
        {
            var taskBadge = _activeBadges.Find(t => t.TaskId == taskId);

            if (taskBadge is null)
            {
                return;
            }
            
            ReleaseTaskBadge(taskBadge);
                
            _activeBadges.Remove(taskBadge);
        }

        private void SetBadgeSprite(MovableObjectTask movableObjectTask,
            TaskBadge freeBadge)
        {
            var (sprite, isBack) = FoundUniqueBadges(movableObjectTask);
            
            if (sprite != null)
            {
                freeBadge.BadgeImage.sprite = sprite;
                freeBadge.BackImage.gameObject.SetActive(isBack);
            }
            else
            {
                var badge = _badgeSprites.First(tbw =>
                    tbw.TaskProgress == movableObjectTask.TaskProgress && tbw.BadgeSprite != null);
                freeBadge.BadgeImage.sprite = badge.BadgeSprite;
                freeBadge.BackImage.gameObject.SetActive(badge.IsBackActive);
            }
        }

        private static (Sprite, bool) FoundUniqueBadges(MovableObjectTask movableObjectTask)
        {
            if (!movableObjectTask.MainObjectView.UniqueBadgesData.HasUniqueBadges)
            {
                return (null, false);
            }

            Sprite uniqueBadgeSprite = null;
            var isBack = false;
                
            foreach (var badge in movableObjectTask.MainObjectView.UniqueBadgesData.UniqueBadges)
            {
                if (badge.TaskProgress != movableObjectTask.TaskProgress 
                    || badge.BadgeSprite is null)
                {
                    continue;
                }

                isBack = badge.IsBackActive;
                uniqueBadgeSprite = badge.BadgeSprite;
                        
                break;
            }

            return (uniqueBadgeSprite, isBack);

        }

        private bool EmptyBadgeFound(MovableObjectTask movableObjectTask, TaskBadge freeBadge)
        {
            if (!NeedToReleaseBadge(movableObjectTask.TaskProgress))
            {
                return false;
            }
            
            ReleaseTaskBadge(freeBadge);

            _activeBadges.Remove(freeBadge);

            return true;
        }
        
        private static bool NeedToReleaseBadge(RuntimeConstants.Enums.TaskProgress taskProgress)
        {
            return taskProgress is RuntimeConstants.Enums.TaskProgress.Cancelled
                or RuntimeConstants.Enums.TaskProgress.Completed;
        }

        private static void DisplayQueuedTaskOrder(MovableObjectTask movableObjectTask, TaskBadge freeBadge, int queuedTaskCount)
        {
            if (movableObjectTask.TaskProgress == RuntimeConstants.Enums.TaskProgress.Queued)
            {
                freeBadge.BadgeText.text = queuedTaskCount.ToString();
                
                return;
            }

            freeBadge.BadgeText.text = string.Empty;
        }
        
        private void SetBadgeData(MovableObjectTask movableObjectTask, TaskBadge freeBadge)
        {
            freeBadge.TaskId = movableObjectTask.TaskId;

            ((RectTransform) freeBadge.Badge.transform).localPosition 
                = UIHelper.ConvertWorldToLocalCanvasPosition(movableObjectTask.MainObjectView.BadgePosition,
                    _sceneReferences.MainCamera, _sceneReferences.MainCanvas);
        }

        private void SetupBadge(MovableObjectTask movableObjectTask, 
            TaskBadge freeBadge, int queuedTaskCount)
        {
            SetBadgeSprite(movableObjectTask, freeBadge);

            if (EmptyBadgeFound(movableObjectTask, freeBadge))
            {
                return;
            }

            DisplayQueuedTaskOrder(movableObjectTask, freeBadge, queuedTaskCount);

            SetBadgeData(movableObjectTask, freeBadge);
        }
        
        private void ReleaseTaskBadge(TaskBadge badge)
        {
            badge.Badge.gameObject.SetActive(false);
            
            _badgePool.Release(badge);
        }

        private TaskBadge GetTaskBadge()
        {
            var badge = _badgePool.Get();
            
            badge.Badge.SetActive(true);
            badge.BadgeImage.gameObject.SetActive(true);
            badge.BackImage.gameObject.SetActive(false);
            
            _activeBadges.Add(badge);

            return badge;
        }


        public void UpdateTaskBadgeOrder()
        {
            _activeBadges.RemoveAll(badge => badge == null || badge.Badge == null);
            
            for (var i = 1; i < _activeBadges.Count; i++)
            {
                if (_activeBadges[i]?.BadgeText == null)
                    continue;
                    
                int.TryParse(_activeBadges[i].BadgeText.text, out var currentCount);

                if (currentCount <= 0)
                {
                    continue;
                }

                _activeBadges[i].BadgeText.text = (currentCount - 1).ToString();
            }
        }
        
        public void UpdateTaskBadgeOrders(List<MovableObjectTask> tasks)
        {
            var taskCount = 0;
            foreach (var task in tasks)
            {
                switch (task.TaskProgress)
                {
                    case RuntimeConstants.Enums.TaskProgress.InProgress when !task.MainObjectView.IsUsing:
                        SetTaskBadge(task, 0);
                        break;
                    case RuntimeConstants.Enums.TaskProgress.Queued:
                        taskCount++;
                        SetTaskBadge(task, taskCount);
                        break;
                    case RuntimeConstants.Enums.TaskProgress.Cancelled:
                    case RuntimeConstants.Enums.TaskProgress.Completed or RuntimeConstants.Enums.TaskProgress.RunHome:
                        var existingBadge = _activeBadges.Find(b => b.TaskId == task.TaskId);
                        if (existingBadge != null)
                        {
                            ReleaseTaskBadge(existingBadge);
                            _activeBadges.Remove(existingBadge);
                        }
                        break;
                    default:
                        var badgeToHide = _activeBadges.Find(b => b.TaskId == task.TaskId);
                        if (badgeToHide != null)
                        {
                            badgeToHide.BadgeImage.gameObject.SetActive(false);
                            badgeToHide.BackImage.gameObject.SetActive(false);
                            badgeToHide.BadgeText.text = string.Empty;
                        }
                        break;
                }
            }
        }
        
        public void SetTaskBadge(MovableObjectTask movableObjectTask, int queuedTaskCount)
        {
            if (NeedToReleaseBadge(movableObjectTask.TaskProgress))
            {
                return;
            }
            
            TryGetBadge(movableObjectTask.TaskId, out var freeBadge);
            
            SetupBadge(movableObjectTask, freeBadge, queuedTaskCount);
        }
        
        public UniTask Load()
        {
            _badgePool = new ObjectPool<TaskBadge>(CreateTaskBadge);

            _badgeSprites = _sceneReferences.GameplaySettings.BadgeSprites;
            
            _reloadController.AddReloadableObject(this);
            
            return UniTask.CompletedTask;
        }

        public UniTask Reload()
        {
            foreach (var taskBadge in _activeBadges)
            {
                ReleaseTaskBadge(taskBadge);
            }
            
            _activeBadges.Clear();
            
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            _reloadController.RemoveReloadableObject(this);
            
            _badgePool.Dispose();
        }
    }
}