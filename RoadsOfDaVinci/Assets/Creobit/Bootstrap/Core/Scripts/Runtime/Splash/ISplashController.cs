using System;
using Creobit.Loading;
using Cysharp.Threading.Tasks;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Splash
{
    public interface ISplashController : ILoadUnit, IDisposable
    {
        public UniTask Show();
    }
}