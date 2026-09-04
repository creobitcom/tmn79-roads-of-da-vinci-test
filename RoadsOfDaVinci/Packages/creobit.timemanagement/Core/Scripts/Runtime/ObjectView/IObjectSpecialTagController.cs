using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskManager;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils.GameplayTags;
using Creobit.Loading;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView
{
    public interface IObjectSpecialTagController : ILoadUnit, IReloadable
    {
        public ITaskObject GetObjectWithTag(GameplayTagSO objectTag, Vector3 closestPoint);
        public bool IsExistsObjectTag(string tagLabel, out ITaskObject[] foundObjects);
        public bool IsExistsBaseTag(string tagLabel, out ITaskObject[] foundObjects);
    }
}