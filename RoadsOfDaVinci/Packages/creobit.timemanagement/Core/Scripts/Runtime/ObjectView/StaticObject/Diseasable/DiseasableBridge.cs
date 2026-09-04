using System;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject.Diseasable
{
    /// <summary>
    /// Единственный на уровень адаптер между Unity-миром и чистым DiseasableController.
    /// Источник вызова — StaticObjectView.OnHeal: его поднимает владелец (HealBase()) когда юнит
    /// реально долечил задачу на его DiseasableView (см. OnEndInteract-подписку в
    /// DiseasableController.InitializeReferences), а не мгновенно по клику. Управляет ВСЕМИ
    /// diseasable-зданиями уровня разом: подписывается на StaticObjectController.AddedStaticObject
    /// и вешает Heal на OnHeal каждого нового объекта — не по инстансу на здание, а один раз на
    /// уровень. Это точка, за которую можно подменить источник вызова, не трогая DiseasableController.
    /// </summary>
    public class DiseasableBridge : IDisposable
    {
        private readonly StaticObjectController _staticObjectController;
        private readonly DiseasableController _diseasableController;

        public DiseasableBridge(StaticObjectController staticObjectController, DiseasableController diseasableController)
        {
            _staticObjectController = staticObjectController;
            _diseasableController = diseasableController;
        }

        public void Load()
        {
            _staticObjectController.AddedStaticObject += HandleStaticObjectAdded;
        }

        private void HandleStaticObjectAdded(StaticObjectView objectView)
        {
            objectView.OnHeal += Heal;
        }

        public void Heal(StaticObjectView objectView)
        {
            _diseasableController.Heal(objectView);
        }

        public void Dispose()
        {
            _staticObjectController.AddedStaticObject -= HandleStaticObjectAdded;

            foreach (var objectView in _staticObjectController.StaticObjects)
            {
                if (objectView != null)
                {
                    objectView.OnHeal -= Heal;
                }
            }
        }
    }
}
