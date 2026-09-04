// UltEvents // https://kybernetik.com.au/ultevents // Copyright 2021-2025 Kybernetik //

using UnityEngine;

namespace UltEvents
{
    /// <summary>
    /// Holds <see cref="UltEvent"/>s which are called by various <see cref="MonoBehaviour"/> lifecycle events:
    /// <see cref="Awake"/>,
    /// <see cref="Start"/>,
    /// <see cref="OnEnable"/>,
    /// <see cref="OnDisable"/>, and
    /// <see cref="OnDestroy"/>.
    /// </summary>
    [AddComponentMenu(UltEventUtils.ComponentMenuPrefix + "Life Cycle Events")]
    [DisallowMultipleComponent]
    [UltEventsHelpUrl(typeof(LifeCycleEvents))]
    public class LifeCycleEvents : MonoBehaviour
    {
        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent _AwakeEvent;

        /// <summary>Invoked by <see cref="Awake"/>.</summary>
        public UltEvent AwakeEvent
        {
            get => _AwakeEvent ??= new();
            set => _AwakeEvent = value;
        }

        /// <summary>Invokes <see cref="AwakeEvent"/>.</summary>
        public virtual void Awake()
            => _AwakeEvent?.Invoke();

        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent _StartEvent;

        /// <summary>Invoked by <see cref="Start"/>.</summary>
        public UltEvent StartEvent
        {
            get => _StartEvent ??= new();
            set => _StartEvent = value;
        }

        /// <summary>Invokes <see cref="StartEvent"/>.</summary>
        public virtual void Start()
            => _StartEvent?.Invoke();

        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent _EnableEvent;

        /// <summary>Invoked by <see cref="OnEnable"/>.</summary>
        public UltEvent EnableEvent
        {
            get => _EnableEvent ??= new();
            set => _EnableEvent = value;
        }

        /// <summary>Invokes <see cref="EnableEvent"/>.</summary>
        public virtual void OnEnable()
            => _EnableEvent?.Invoke();

        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent _DisableEvent;

        /// <summary>Invoked by <see cref="OnDisable"/>.</summary>
        public UltEvent DisableEvent
        {
            get => _DisableEvent ??= new();
            set => _DisableEvent = value;
        }

        /// <summary>Invokes <see cref="DisableEvent"/>.</summary>
        public virtual void OnDisable()
            => _DisableEvent?.Invoke();

        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent _DestroyEvent;

        /// <summary>Invoked by <see cref="OnDestroy"/>.</summary>
        public UltEvent DestroyEvent
        {
            get => _DestroyEvent ??= new();
            set => _DestroyEvent = value;
        }

        /// <summary>Invokes <see cref="DestroyEvent"/>.</summary>
        public virtual void OnDestroy()
            => _DestroyEvent?.Invoke();

        /************************************************************************************************************************/
    }
}