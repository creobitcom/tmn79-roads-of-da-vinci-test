// UltEvents // https://kybernetik.com.au/ultevents // Copyright 2021-2025 Kybernetik //

#if UNITY_PHYSICS_3D

using UnityEngine;

namespace UltEvents
{
    /// <summary>
    /// Holds <see cref="UltEvent"/>s which are called by various <see cref="MonoBehaviour"/> collision events:
    /// <see cref="OnCollisionEnter"/>,
    /// <see cref="OnCollisionStay"/>, and
    /// <see cref="OnCollisionExit"/>.
    /// </summary>
    [AddComponentMenu(UltEventUtils.ComponentMenuPrefix + "Collision Events 3D")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [UltEventsHelpUrl(typeof(CollisionEvents3D))]
    public class CollisionEvents3D : MonoBehaviour
    {
        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent<Collision> _CollisionEnterEvent;

        /// <summary>Invoked by <see cref="OnCollisionEnter"/>.</summary>
        public UltEvent<Collision> CollisionEnterEvent
        {
            get => _CollisionEnterEvent ??= new();
            set => _CollisionEnterEvent = value;
        }

        /// <summary>Invokes <see cref="CollisionEnterEvent"/>.</summary>
        public virtual void OnCollisionEnter(Collision collision)
            => _CollisionEnterEvent?.Invoke(collision);

        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent<Collision> _CollisionStayEvent;

        /// <summary>Invoked by <see cref="OnCollisionStay"/>.</summary>
        public UltEvent<Collision> CollisionStayEvent
        {
            get => _CollisionStayEvent ??= new();
            set => _CollisionStayEvent = value;
        }

        /// <summary>Invokes <see cref="CollisionStayEvent"/>.</summary>
        public virtual void OnCollisionStay(Collision collision)
            => _CollisionStayEvent?.Invoke(collision);

        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent<Collision> _CollisionExitEvent;

        /// <summary>Invoked by <see cref="OnCollisionExit"/>.</summary>
        public UltEvent<Collision> CollisionExitEvent
        {
            get => _CollisionExitEvent ??= new();
            set => _CollisionExitEvent = value;
        }

        /// <summary>Invokes <see cref="CollisionExitEvent"/>.</summary>
        public virtual void OnCollisionExit(Collision collision)
            => _CollisionExitEvent?.Invoke(collision);

        /************************************************************************************************************************/
    }
}

#endif