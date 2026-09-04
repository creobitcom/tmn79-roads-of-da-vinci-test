using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.Transport
{
    /// <summary>
    /// Владение юнитом на время перевозки транспортом (лифт, фуникулёр, портал).
    ///
    /// Инвариант: пока удержание живо, ТОЛЬКО его владелец меняет позицию, разворот и
    /// анимацию юнита. Остальная игра может менять его намерение (цель, задача) —
    /// оно вступает в силу в момент отпускания.
    ///
    /// Собран из уже существующих механизмов вьюхи, ничего нового в поведение не вносит:
    /// movement lock (скорость 0 + ротатор сам выходит по IsMovementLocked),
    /// temporary animation (PlayAnimation становится no-op, но пишет логическое состояние),
    /// canMove = false (агент отцеплен от трансформа).
    /// </summary>
    public sealed class MovableObjectTransportHold : IDisposable
    {
        /// <summary>Разворот «лицом к игроку» — тот же угол, что по умолчанию у StandingPointView.</summary>
        public const float LookAtCameraAngle = 180f;

        private readonly MovableObjectView _unit;
        private readonly object _owner;

        private bool _released;

        public MovableObjectView Unit => _unit;
        public bool IsAlive => !_released && _unit != null;

        private MovableObjectTransportHold(MovableObjectView unit, object owner)
        {
            _unit = unit;
            _owner = owner;
        }

        /// <summary>
        /// Пытается взять юнита под перевозку. Возвращает null, если юнит уже кем-то удержан
        /// (стан Humorist, другой транспорт) или не готов — тогда транспорт обязан просто
        /// пропустить его в этом кадре. Полузахваченного состояния не бывает: любой отказ
        /// на середине откатывает всё, что успели взять.
        /// </summary>
        public static MovableObjectTransportHold TryAcquire(MovableObjectView unit,
            object owner,
            float lookAngle = LookAtCameraAngle,
            string idleStateName = RuntimeConstants.AnimationStates.Idle)
        {
            if (unit == null || owner == null || unit.AStarAI == null || unit.modelTransform == null)
            {
                return null;
            }

            if (unit.IsHeldByTransport || unit.IsMovementLocked)
            {
                return null;
            }

            if (!unit.TryBeginTransportHold(owner))
            {
                return null;
            }

            // Владение анимацией забирается ПЕРВЫМ, до движения, и порядок здесь критичен:
            // TryAcquireMovementLock внутри зовёт SetSpeed -> PlayAnimation(_currentAnimation),
            // и пока владения нет, это ставит триггер Run. Animator.Play(idle) следом переключит
            // стейт, но висящий триггер никуда не денется и на ближайшей оценке аниматора вернёт
            // юнита обратно в бег — он будет бежать на месте всю поездку.
            //
            // Имя стейта приходит снаружи, а не константой: Animator.Play матчит имя точно,
            // а оно зависит от проекта (у HumoristAttackSettingsSO дефолт вообще "idle").
            // Промах именем = юнит молча остаётся в беге, то есть ровно тот баг, который чиним.
            if (!unit.TryBeginTemporaryAnimation(owner, idleStateName))
            {
                unit.EndTransportHold(owner);
                return null;
            }

            if (!unit.TryAcquireMovementLock(owner))
            {
                unit.EndTemporaryAnimation(owner);
                unit.EndTransportHold(owner);
                return null;
            }

            // Логическая анимация. Под чужим владением PlayAnimation до аниматора не доходит,
            // но делает две нужные вещи: сбрасывает висящий триггер прошлой анимации (Run)
            // и запоминает Idle как текущую — её и восстановит ReleaseMovementLock на выходе.
            // Повторный Play фиксирует стейт уже после всей возни с триггерами.
            unit.PlayAnimation(idleStateName);
            unit.PlayTemporaryAnimation(owner, idleStateName);

            // Агент отцепляется от трансформа целиком. AIBase.Update при canMove == false
            // пропускает MovementUpdate, а значит: юнита никто не двигает, интерполятор стоит,
            // и длинносегментный телепорт AITMNPath не может перекинуть юнита через пролёт.
            // Путь при этом продолжает считаться и читаться (GetRemainingPath работает),
            // поэтому транспорт может решать, нужен ли юниту переезд.
            unit.AStarAI.canMove = false;

            var hold = new MovableObjectTransportHold(unit, owner);
            hold.SetFacing(lookAngle);

            return hold;
        }

        /// <summary>
        /// Запрет на выдачу новых задач. Ставится только на саму поездку: пока юнит ждёт
        /// кабину, он остаётся обычным свободным рабочим и может получить приказ.
        /// </summary>
        public void SetSealed(bool isSealed)
        {
            if (!IsAlive) return;

            _unit.SetTransportSealed(_owner, isSealed);
        }

        public void SetPosition(Vector3 position)
        {
            if (!IsAlive) return;

            _unit.transform.position = position;
        }

        public void SetFacing(float lookAngle)
        {
            if (!IsAlive || _unit.modelTransform == null) return;

            _unit.modelTransform.rotation = _unit.initialRotation * Quaternion.AngleAxis(lookAngle, Vector3.up);
        }

        /// <summary>
        /// Сменить анимацию, не теряя владения (например, бег на высадке). Логическая и
        /// фактическая ставятся вместе: иначе на выходе ReleaseMovementLock восстановил бы
        /// логическую, а она осталась бы от момента захвата — юнит уехал бы «стоя».
        /// </summary>
        public void PlayAnimation(string stateName)
        {
            if (!IsAlive) return;

            _unit.PlayAnimation(stateName);
            _unit.PlayTemporaryAnimation(_owner, stateName);
        }

        /// <summary>Отпускает юнита, поставив его в точку высадки. Единственный телепорт за перевозку.</summary>
        public void ReleaseAt(Vector3 landingPosition) => ReleaseInternal(landingPosition);

        public void Release() => ReleaseInternal(null);

        public void Dispose() => ReleaseInternal(null);

        private void ReleaseInternal(Vector3? landingPosition)
        {
            if (_released) return;
            _released = true;

            // Юнит уничтожен — освобождать нечего, флаги умерли вместе с ним.
            if (_unit == null) return;

            // Порядок обратный захвату. Агент возвращается первым, чтобы Teleport отработал
            // штатно: он чистит путь и сам перезапрашивает его к неизменной destination —
            // юнит продолжит ровно туда, куда шёл или куда его перенаправили во время поездки.
            if (_unit.AStarAI != null)
            {
                _unit.AStarAI.canMove = true;

                if (landingPosition.HasValue)
                {
                    _unit.AStarAI.Teleport(landingPosition.Value);
                }
            }

            _unit.EndTemporaryAnimation(_owner);
            _unit.ReleaseMovementLock(_owner);
            _unit.EndTransportHold(_owner);
        }
    }
}
