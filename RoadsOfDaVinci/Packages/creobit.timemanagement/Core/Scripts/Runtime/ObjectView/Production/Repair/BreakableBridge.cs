using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.Production.Repair
{
    /// <summary>
    /// Единственный на уровень адаптер между Unity-миром и чистым BreakableController.
    /// Источник вызова — StaticObjectView.OnRepair: его поднимает владелец (RepairProduction())
    /// когда юнит реально доработал задачу на его BreakableView (см. OnEndInteract-подписку
    /// в BreakableController.Initialize), а не мгновенно по клику. Управляет ВСЕМИ breakable-зданиями
    /// уровня разом: подписывается на StaticObjectController.AddedStaticObject и вешает Repair на
    /// OnRepair каждого нового объекта — не по инстансу на здание, а один раз на уровень. Это точка,
    /// за которую можно подменить источник вызова, не трогая сам BreakableController.
    /// </summary>
    public class BreakableBridge : IDisposable
    {
        private readonly StaticObjectController _staticObjectController;
        private readonly BreakableController _breakableController;

        public BreakableBridge(StaticObjectController staticObjectController, BreakableController breakableController)
        {
            _staticObjectController = staticObjectController;
            _breakableController = breakableController;
        }

        public void Load()
        {
            _staticObjectController.AddedStaticObject += HandleStaticObjectAdded;
        }

        private void HandleStaticObjectAdded(StaticObjectView objectView)
        {
            objectView.OnRepair += Repair;
        }

        public void Repair(StaticObjectView objectView)
        {
            _breakableController.Repair(objectView);
        }

        public void Dispose()
        {
            _staticObjectController.AddedStaticObject -= HandleStaticObjectAdded;

            foreach (var objectView in _staticObjectController.StaticObjects)
            {
                if (objectView != null)
                {
                    objectView.OnRepair -= Repair;
                }
            }
        }
    }
}
