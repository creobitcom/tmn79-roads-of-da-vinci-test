// UltEvents // https://kybernetik.com.au/ultevents // Copyright 2021-2025 Kybernetik //

#if UNITY_PHYSICS_3D

using UnityEngine;

namespace UltEvents
{
    /// <summary>
    /// Holds <see cref="UltEvent"/>s which are called by various <see cref="MonoBehaviour"/> trigger events:
    /// <see cref="OnTriggerEnter"/>,
    /// <see cref="OnTriggerStay"/>, and
    /// <see cref="OnTriggerExit"/>.
    /// </summary>
    [AddComponentMenu(UltEventUtils.ComponentMenuPrefix + "Trigger Events 3D")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [UltEventsHelpUrl(typeof(TriggerEvents3D))]
    public class TriggerEvents3D : MonoBehaviour
    {
        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent<Collider> _TriggerEnterEvent;

        /// <summary>Invoked by <see cref="OnTriggerEnter"/>.</summary>
        public UltEvent<Collider> TriggerEnterEvent
        {
            get => _TriggerEnterEvent ??= new();
            set => _TriggerEnterEvent = value;
        }

        /// <summary>Invokes <see cref="TriggerEnterEvent"/>.</summary>
        public virtual void OnTriggerEnter(Collider collider)
            => _TriggerEnterEvent?.Invoke(collider);

        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent<Collider> _TriggerStayEvent;

        /// <summary>Invoked by <see cref="OnTriggerStay"/>.</summary>
        public UltEvent<Collider> TriggerStayEvent
        {
            get => _TriggerStayEvent ??= new();
            set => _TriggerStayEvent = value;
        }

        /// <summary>Invokes <see cref="TriggerStayEvent"/>.</summary>
        public virtual void OnTriggerStay(Collider collider)
            => _TriggerStayEvent?.Invoke(collider);

        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent<Collider> _TriggerExitEvent;

        /// <summary>Invoked by <see cref="OnTriggerExit"/>.</summary>
        public UltEvent<Collider> TriggerExitEvent
        {
            get => _TriggerExitEvent ??= new();
            set => _TriggerExitEvent = value;
        }

        /// <summary>Invokes <see cref="TriggerExitEvent"/>.</summary>
        public virtual void OnTriggerExit(Collider collider)
            => _TriggerExitEvent?.Invoke(collider);

        /************************************************************************************************************************/
    }
}

#endif