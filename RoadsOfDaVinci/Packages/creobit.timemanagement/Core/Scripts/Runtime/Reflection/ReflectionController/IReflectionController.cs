using System;
using Creobit.Loading;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Reflection.ReflectionController
{
    public interface IReflectionController : ILoadUnit, IDisposable
    {
        void AddReflection(LaserEmitterView view);
    }
}