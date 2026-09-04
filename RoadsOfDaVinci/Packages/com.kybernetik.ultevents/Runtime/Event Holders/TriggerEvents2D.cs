// UltEvents // https://kybernetik.com.au/ultevents // Copyright 2021-2025 Kybernetik //

#if UNITY_PHYSICS_2D

using UnityEngine;

namespace UltEvents
{
    /// <summary>
    /// Holds <see cref="UltEvent"/>s which are called by various <see cref="MonoBehaviour"/> 2D trigger events:
    /// <see cref="OnTriggerEnter2D"/>,
    /// <see cref="OnTriggerStay2D"/>, and
    /// <see cref="OnTriggerExit2D"/>.
    /// </summary>
    [AddComponentMenu(UltEventUtils.ComponentMenuPrefix + "Trigger Events 2D")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [UltEventsHelpUrl(typeof(TriggerEvents2D))]
    public class TriggerEvents2D : MonoBehaviour
    {
        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent<Collider2D> _TriggerEnterEvent;

        /// <summary>Invoked by <see cref="OnTriggerEnter2D"/>.</summary>
        public UltEvent<Collider2D> TriggerEnterEvent
        {
            get => _TriggerEnterEvent ??= new();
            set => _TriggerEnterEvent = value;
        }

        /// <summary>Invokes <see cref="TriggerEnterEvent"/>.</summary>
        public virtual void OnTriggerEnter2D(Collider2D collider)
            => _TriggerEnterEvent?.Invoke(collider);

        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent<Collider2D> _TriggerStayEvent;

        /// <summary>Invoked by <see cref="OnTriggerStay2D"/>.</summary>
        public UltEvent<Collider2D> TriggerStayEvent
        {
            get => _TriggerStayEvent ??= new();
            set => _TriggerStayEvent = value;
        }

        /// <summary>Invokes <see cref="TriggerStayEvent"/>.</summary>
        public virtual void OnTriggerStay2D(Collider2D collider)
            => _TriggerStayEvent?.Invoke(collider);

        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent<Collider2D> _TriggerExitEvent;

        /// <summary>Invoked by <see cref="OnTriggerExit2D"/>.</summary>
        public UltEvent<Collider2D> TriggerExitEvent
        {
            get => _TriggerExitEvent ??= new();
            set => _TriggerExitEvent = value;
        }

        /// <summary>Invokes <see cref="TriggerExitEvent"/>.</summary>
        public virtual void OnTriggerExit2D(Collider2D collider)
            => _TriggerExitEvent?.Invoke(collider);

        /************************************************************************************************************************/
    }
}

#endif