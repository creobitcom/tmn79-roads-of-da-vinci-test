using System;
using System.Collections.Generic;
using System.Reflection;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Utility
{
    public static class ReflectionUtility
    {
        public static List<T> GetConcreteClasses<T>()
            where T : class
        {
            var concreteClasses = new List<T>();

            var assembly = Assembly.GetAssembly(typeof(T));

            foreach (var concreteType in assembly.GetTypes())
                if (typeof(T).IsAssignableFrom(concreteType) && !concreteType.IsAbstract && !concreteType.IsInterface)
                {
                    concreteClasses.Add((T)Activator.CreateInstance(concreteType));
                }

            return concreteClasses;
        }
    }
}