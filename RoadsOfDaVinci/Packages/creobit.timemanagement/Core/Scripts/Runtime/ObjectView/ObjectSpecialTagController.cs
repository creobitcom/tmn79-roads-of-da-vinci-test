using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Cysharp.Threading.Tasks;
using UnityEngine;
using ITaskObject = _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager.ITaskObject;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView
{
    public class ObjectSpecialTagController : IReloadable
    {
        private readonly StaticObjectController _staticObjectController;
        private readonly IReloadController _reloadController;

        private readonly Dictionary<GameplayTagSO, List<ITaskObject>> _objectsWithTags = new();
        private readonly Dictionary<GameplayTagSO, List<ITaskObject>> _basesWithTags = new();

        public ObjectSpecialTagController(StaticObjectController staticObjectController,
            IReloadController reloadController)
        {
            _staticObjectController = staticObjectController;
            _reloadController = reloadController;
        }

        public UniTask Load()
        {
            _staticObjectController.AddedStaticObject += StaticObjectAddedHandler;
            
            _reloadController.AddReloadableObject(this);

            return UniTask.CompletedTask;
        }

        public UniTask Reload()
        {
            _objectsWithTags.Clear();
            _basesWithTags.Clear();
            return UniTask.CompletedTask;
        }
        
        public ITaskObject GetObjectWithTag(GameplayTagSO objectTag, Vector3 closestPoint)
        {
            ITaskObject closestObject = null;

            foreach (var objectView in _objectsWithTags[objectTag])
            {
                if (closestObject == null
                    || Vector3.Distance(objectView.Position, closestPoint) <
                    Vector3.Distance(closestObject.Position, closestPoint))
                {
                    closestObject = objectView;
                }
            }

            return closestObject;
        }

        public ITaskObject GetComplexObjectWithTag(GameplayTagSO objectTag, Vector3 closestPoint)
        {
            ITaskObject closestObject = null;

            foreach (var objectView in _objectsWithTags[objectTag])
            {
                var gameObj = (objectView as ObjectView)?.transform.GetComponentInParent<ComplexObject>();
                if (!gameObj)
                {
                    continue;
                }
                
                if (closestObject == null
                    || Vector3.Distance(objectView.Position, closestPoint) <
                    Vector3.Distance(closestObject.Position, closestPoint))
                {
                    closestObject = gameObj;
                }
            }

            return closestObject;
        }
        
        public bool IsExistsObjectTag(string tagLabel, out ITaskObject[] foundObjects)
        {
            foundObjects = System.Array.Empty<ITaskObject>();

            foreach (var tag in _objectsWithTags.Keys)
            {
                if (tag.name == tagLabel)
                {
                    foundObjects = _objectsWithTags[tag].ToArray();
                    return foundObjects.Length > 0;
                }
            }

            return false;
        }

        public bool IsExistsBaseTag(string tagLabel, out ITaskObject[] foundObjects)
        {
            foundObjects = System.Array.Empty<ITaskObject>();

            foreach (var tag in _basesWithTags.Keys)
            {
                if (tag.name == tagLabel)
                {
                    foundObjects = _basesWithTags[tag].ToArray();
                    return foundObjects.Length > 0;
                }
            }

            return false;
        }

        private void StaticObjectAddedHandler(StaticObjectView staticObjectView)
        {
            foreach (var tag in staticObjectView.ObjectDataSO.ObjectTypeTags)
            {
                if (tag.TagType == TagType.Object)
                {
                    if (!_objectsWithTags.ContainsKey(tag))
                    {
                        _objectsWithTags.Add(tag, new List<ITaskObject>());
                    }

                    _objectsWithTags[tag].Add(staticObjectView);
                }
            }

            foreach (var baseType in staticObjectView.BaseData.BaseTypeTags)
            {
                if (baseType == null)
                    continue;
                
                if (!_basesWithTags.ContainsKey(baseType))
                {
                    _basesWithTags.Add(baseType, new List<ITaskObject>());
                }
                
                _basesWithTags[baseType].Add(staticObjectView);
            }
        }
    }
}