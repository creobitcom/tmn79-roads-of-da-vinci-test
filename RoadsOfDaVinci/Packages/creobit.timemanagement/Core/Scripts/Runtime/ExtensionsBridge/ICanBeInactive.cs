#if TIME_MANAGER_INACTIVE_OBJECT
using _8floor.TimeManagement.Extensions.Inactive.Core.Settings;
#endif

using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ExtensionsBridge
{
    public interface ICanBeInactive
    {
        public Vector3 Position { get; }
        
        public short Active { get; set; }

#if TIME_MANAGER_INACTIVE_OBJECT
		public void SetInactiveSettings(InactiveObjectSettings inactiveObjectSettings);
#endif
	}
}