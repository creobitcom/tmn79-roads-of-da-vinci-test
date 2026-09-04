using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects
{
    public class MovableObjectBridge : MonoBehaviour
    {
        /// <summary>
        /// Set run animation as current.
        /// </summary>
        /// <param name="unit">Unit to modify.</param>
        /// <param name="animationName">Run animation name.</param>
        public void SetRunAnimation(MovableObjectView unit, string animationName)
        {
            unit.SetRunAnimation(animationName);
        }

        /// <summary>
        /// Set run animation as current.
        /// </summary>
        /// <param name="unit">Unit to modify.</param>
        public void ResetRunAnimation(MovableObjectView unit)
        {
            unit.ResetRunAnimation();
        }
    }
}