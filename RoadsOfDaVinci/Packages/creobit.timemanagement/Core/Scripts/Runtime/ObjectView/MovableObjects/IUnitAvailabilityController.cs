using _8floor.TimeManagement.Core.Scripts.Runtime.BaseController.Data;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects
{
    /// <summary>
    /// Interface for managing unit availability on a base.
    /// </summary>
    public interface IUnitAvailabilityController
    {
        /// <summary>
        /// Adds a restriction to the number of available units on a base.
        /// </summary>
        /// <param name="baseTransform">The base transform.</param>
        /// <param name="restrictedUnits">The number added of units to restrict.</param>
        /// <remarks>
        /// If no restrictions exist, the number will be assigned as a new restriction.
        /// </remarks>
        void AddRestriction(Transform baseTransform, int restrictedUnits);

        /// <summary>
        /// Returns the number of available units on a base, taking restrictions.
        /// </summary>
        /// <param name="baseTransform">The base transform.</param>
        /// <returns>The number of available units.</returns>
        int GetAvailableUnitCount(Transform baseTransform);

        /// <summary>
        /// Registers a base and its data.
        /// </summary>
        /// <param name="baseTransform">The base transform.</param>
        /// <param name="baseData">The base data.</param>
        void RegisterBase(Transform baseTransform, BaseData baseData);

        /// <summary>
        /// Removes a validated restriction from the number of available units on a base.
        /// </summary>
        /// <param name="baseTransform">The base transform.</param>
        /// <param name="restrictedUnits">The number of restrictions to remove.</param>
        void RemoveRestriction(Transform baseTransform, int restrictedUnits);

        /// <summary>
        /// Returns the number of restricted units on a base.
        /// </summary>
        /// <param name="baseTransform">The base transform.</param>
        /// <returns>The number of restricted units.</returns>
        int GetRestrictedUnitCount(Transform baseTransform);
    }
}