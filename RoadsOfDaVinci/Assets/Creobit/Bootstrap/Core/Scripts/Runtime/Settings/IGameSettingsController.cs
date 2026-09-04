using System;
using Creobit.Loading;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Settings
{
    public interface IGameSettingsController : ILoadUnit, IDisposable
    {
        GameSettings Settings { get; }
    }
}