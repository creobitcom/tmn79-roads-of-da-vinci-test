using System.Collections.Generic;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Herding
{
    /// <summary>
    /// Точка назначения для <see cref="HerdableObject"/> — «загон».
    ///
    /// Считается доступной, пока GameObject с этим компонентом активен. Включать его нужно тогда,
    /// когда загон готов принимать: либо положить компонент прямо на восстановленное состояние
    /// COC-объекта (оно включается само при апгрейде), либо повесить включение на UltEvent
    /// onEndInteract разрушенного загона (SetActive(true)).
    /// </summary>
    [DisallowMultipleComponent]
    public class HerdSpot : MonoBehaviour
    {
        [SerializeField, Tooltip("Куда встаёт уведённый объект. Пусто — сам transform.")]
        private Transform _arrivalPoint;

        [SerializeField, Tooltip("Куда встаёт сопровождающий юнит. Пусто — точка прибытия.")]
        private Transform _escortPoint;

        [SerializeField, Min(0), Tooltip("Сколько объектов вмещает загон. 0 — без ограничения.")]
        private int _capacity;

        private static readonly List<HerdSpot> Spots = new List<HerdSpot>();

        private int _occupied;

        public Transform ArrivalPoint => _arrivalPoint != null ? _arrivalPoint : transform;

        public Transform EscortPoint => _escortPoint != null ? _escortPoint : ArrivalPoint;

        public bool HasRoom => _capacity <= 0 || _occupied < _capacity;

        /// <summary>
        /// Занять место. Вызывается в момент старта перегона, а не прибытия — иначе два объекта
        /// уйдут в загон вместимостью 1.
        /// </summary>
        public void Occupy() => _occupied++;

        public void Free() => _occupied = Mathf.Max(0, _occupied - 1);

        /// <summary>Есть ли хоть один активный загон со свободным местом.</summary>
        public static bool HasAvailable()
        {
            for (var i = Spots.Count - 1; i >= 0; i--)
            {
                var spot = Spots[i];
                if (spot == null)
                {
                    Spots.RemoveAt(i);
                    continue;
                }

                if (spot.HasRoom) return true;
            }

            return false;
        }

        /// <summary>Ближайший по прямой доступный загон. null — если доступных нет.</summary>
        public static HerdSpot FindNearestAvailable(Vector3 from)
        {
            HerdSpot best = null;
            var bestDistance = float.MaxValue;

            for (var i = Spots.Count - 1; i >= 0; i--)
            {
                var spot = Spots[i];
                if (spot == null)
                {
                    Spots.RemoveAt(i);
                    continue;
                }

                if (!spot.HasRoom) continue;

                var distance = (spot.ArrivalPoint.position - from).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = spot;
            }

            return best;
        }

        private void OnEnable()
        {
            if (!Spots.Contains(this)) Spots.Add(this);
        }

        private void OnDisable()
        {
            Spots.Remove(this);
        }
    }
}
