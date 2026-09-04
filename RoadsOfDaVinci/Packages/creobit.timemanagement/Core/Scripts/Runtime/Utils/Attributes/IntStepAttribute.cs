using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils.Attributes
{
    public class IntStepAttribute : PropertyAttribute
    {
        public float Step { get; }

        public IntStepAttribute(float step)
        {
            Step = step;
        }
    }
}

