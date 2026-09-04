// UltEvents // https://kybernetik.com.au/ultevents // Copyright 2021-2025 Kybernetik //

using UnityEngine;

namespace UltEvents
{
    /// <summary>
    /// A component which encapsulates a single <see cref="UltEvent"/>.
    /// </summary>
    [AddComponentMenu(UltEventUtils.ComponentMenuPrefix + "Ult Event Holder")]
    [UltEventsHelpUrl(typeof(UltEventHolder))]
    public class UltEventHolder : MonoBehaviour
    {
        /************************************************************************************************************************/

        [SerializeField]
        private UltEvent _Event;

        /// <summary>The encapsulated event.</summary>
        public UltEvent Event
        {
            get => _Event ??= new();
            set => _Event = value;
        }

        /// <summary>Invoked the <see cref="Event"/>.</summary>
        public virtual void Invoke()
            => _Event?.Invoke();

        /************************************************************************************************************************/
    }
}