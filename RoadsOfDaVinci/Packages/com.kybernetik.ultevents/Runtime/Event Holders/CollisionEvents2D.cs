// UltEvents // https://kybernetik.com.au/ultevents // Copyright 2021-2025 Kybernetik //

#if UNITY_PHYSICS_2D

using UnityEngine;

namespace UltEvents
{
    /// <summary>
    /// Holds <see cref="UltEvent"/>s which are called by various <see cref="MonoBehaviour"/> 2D collision events:
    /// <see cref="OnCollisionEnter2D"/>,
    /// <see cref="OnCollisionStay2D"/>, and
    /// <see cref="OnCollisionExit2D"/>.
    /// </summary>
    [AddComponentMenu(UltEventUtils.ComponentMenuPrefix + "Collision Events 2D")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [UltEventsHelpUrl(typeof(CollisionEvents2D))]
    public class CollisionEvents2D : MonoBehaviour
    {
        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent<Collision2D> _CollisionEnterEvent;

        /// <summary>Invoked by <see cref="OnCollisionEnter2D"/>.</summary>
        public UltEvent<Collision2D> CollisionEnterEvent
        {
            get => _CollisionEnterEvent ??= new();
            set => _CollisionEnterEvent = value;
        }

        /// <summary>Invokes <see cref="CollisionEnterEvent"/>.</summary>
        public virtual void OnCollisionEnter2D(Collision2D collision)
            => _CollisionEnterEvent?.Invoke(collision);

        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent<Collision2D> _CollisionStayEvent;

        /// <summary>Invoked by <see cref="OnCollisionStay2D"/>.</summary>
        public UltEvent<Collision2D> CollisionStayEvent
        {
            get => _CollisionStayEvent ??= new();
            set => _CollisionStayEvent = value;
        }

        /// <summary>Invokes <see cref="CollisionStayEvent"/>.</summary>
        public virtual void OnCollisionStay2D(Collision2D collision)
            => _CollisionStayEvent?.Invoke(collision);

        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent<Collision2D> _CollisionExitEvent;

        /// <summary>Invoked by <see cref="OnCollisionExit2D"/>.</summary>
        public UltEvent<Collision2D> CollisionExitEvent
        {
            get => _CollisionExitEvent ??= new();
            set => _CollisionExitEvent = value;
        }

        /// <summary>Invokes <see cref="CollisionExitEvent"/>.</summary>
        public virtual void OnCollisionExit2D(Collision2D collision)
            => _CollisionExitEvent?.Invoke(collision);

        /************************************************************************************************************************/
    }
}

#endif