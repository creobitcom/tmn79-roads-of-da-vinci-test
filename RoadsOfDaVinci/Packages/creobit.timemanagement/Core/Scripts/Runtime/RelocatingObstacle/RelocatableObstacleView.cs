using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using Pathfinding;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.RelocatingObstacle
{
    /// <summary>
    /// Вьюшка препятствия, которое умеет переезжать на другой нод графа.
    /// Ведущий компонент — <see cref="RelocatingObstacleService"/> на корне префаба.
    /// (GG6: кочка, в оригинале класс MovableObstacle.)
    ///
    /// Почему наследник, а не правка <see cref="StaticObjectView"/>: базовый InitGraphNode
    /// приватный и вызывается один раз из Load(), нод кэшируется в _interactionGraphNode.
    /// Для переезда нод надо перерешать по новой позиции. Все нужные поля в ObjectView
    /// protected, поэтому наследник делает это сам — StaticObjectView (605 префабов в проекте)
    /// остаётся нетронутым.
    ///
    /// ВАЖНО про пенальти: базовый InitGraphNode внутри зовёт ConvertUnits(), который умножает
    /// _defaultNodePenalty и _blockedNodePenalty на 1000. Повторно вызывать его нельзя —
    /// пенальти раздувалось бы ×1000 за каждый переезд, и A* начал бы обходить пол-уровня.
    /// Здесь своя привязка без ConvertUnits: значения уже сконвертированы базой на Load().
    ///
    /// ВАЖНО про баланс счётчиков: базовый ApplyFinalInteractionEffects при гашении объекта
    /// снимает только Blocked и _blockedNodePenalty, а _defaultNodePenalty остаётся висеть
    /// на ноде. Для одноразового препятствия это неважно, для переезжающего — накапливалось бы
    /// на каждом посещённом ноде. Поэтому <see cref="ReleaseGraphNode"/> снимает и его.
    /// </summary>
    public class RelocatableObstacleView : StaticObjectView
    {
        /// Контроллер нужен ведущему сервису (список юнитов, список статических объектов).
        /// В ObjectView поле protected — наружу его отдаёт наследник, без правки базы.
        public IObjectViewController Controller => ObjectViewController;

        /// <summary>
        /// Полностью снять свой вклад с текущей ноды — перед тем как уехать.
        /// Идемпотентно: блок снимается только если мы его держим (после штатного гашения
        /// объекта базой он уже снят), дефолтное пенальти — только если нода привязана.
        /// </summary>
        public void ReleaseGraphNode()
        {
            if (!_useInteractionGraphNode || _interactionGraphNode == null)
            {
                return;
            }

            if (isBlocking)
            {
                isBlocking = false;
                _interactionGraphNode.Blocked--;
                SubtractPenalty(_blockedNodePenalty);
            }

            SubtractPenalty(_defaultNodePenalty);
            _interactionGraphNode.Dirty = true;
            _interactionGraphNode = null;
        }

        /// <summary>
        /// Привязаться к ноде в текущей позиции и снова закрыть дорогу.
        /// Зеркалит базовый InitGraphNode, но без ConvertUnits (см. комментарий класса).
        /// </summary>
        public void RebindGraphNode()
        {
            if (!_useInteractionGraphNode)
            {
                interactionPosition = transform.position;
                return;
            }

            if (AstarPath.active == null)
            {
                Debug.LogError($"[RelocatingObstacle] {name}: AstarPath.active == null, ноду не привязать.", this);
                return;
            }

            interactionPosition = _useNearestInteractionGraphNode || _interactionGraphNodeTransform == null
                ? transform.position
                : _interactionGraphNodeTransform.position;

            var constraint = NNConstraint.None;
            constraint.graphMask = 1 << 0; // тот же граф точек, что и в базовом InitGraphNode
            _interactionGraphNode = AstarPath.active.GetNearest(interactionPosition, constraint).node;

            if (_interactionGraphNode == null)
            {
                Debug.LogError($"[RelocatingObstacle] {name}: в позиции {interactionPosition} нет ноды графа.", this);
                return;
            }

            _interactionGraphNode.Penalty += _defaultNodePenalty;

            if (ObjectDataSO.BlocksPath)
            {
                _interactionGraphNode.Blocked++;
                _interactionGraphNode.Penalty += _blockedNodePenalty;
                isBlocking = true;
            }

            _interactionGraphNode.Dirty = true;
        }

        /// <summary>
        /// Снова сделать препятствие рабочим: показать, разрешить клик, закрыть дорогу в новой точке.
        ///
        /// Счётчик взаимодействий сбрасываем сами. База обнуляет его только когда гасит объект
        /// (DeactivateAfterFinalInteraction), а у переезжающего препятствия гашения нет — иначе
        /// аниматор при включении запомнил бы позу конца анимации ухода как дефолтную. Без сброса
        /// IsFinalInteraction() остаётся истинным, и ObjectViewController отказывает в новой
        /// задаче: препятствие видно и дорогу держит, но не кликается.
        /// </summary>
        public void Rearm()
        {
            gameObject.SetActive(true);
            ResetInteractionsCount();
            CanInteract = true;
            RebindGraphNode();
        }

        private void SubtractPenalty(uint amount)
        {
            var penalty = (int)(_interactionGraphNode.Penalty - amount);
            _interactionGraphNode.Penalty = penalty >= 0 ? (uint)penalty : 0;
        }
    }
}
