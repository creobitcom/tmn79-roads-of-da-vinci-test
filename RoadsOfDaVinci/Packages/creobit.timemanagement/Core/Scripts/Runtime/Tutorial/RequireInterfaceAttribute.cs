using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial
{
    public class RequireInterfaceAttribute : PropertyAttribute
    {
        public System.Type requiredType { get; private set; }
        public RequireInterfaceAttribute(System.Type type)
        {
            this.requiredType = type;
        }
    }
}