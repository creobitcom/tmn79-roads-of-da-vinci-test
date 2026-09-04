using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Loading;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject
{
    public interface IStaticObjectSystem : ILoadUnit, IReloadable, IDisposable
    {
        public IReadOnlyList<StaticObjectView> StaticObjects { get; }
        public event Action<StaticObjectView> AddedStaticObject;
        public event Action<StaticObjectView> RemovedStaticObject;
        public void Enable();
        public void AddStaticObject(StaticObjectView staticObjectView);
        public void RemoveStaticObject(StaticObjectView staticObjectView);
    }
}