using Cysharp.Threading.Tasks;
using Application = UnityEngine.Application;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.AppDataConfigurationSystem
{
    public sealed class AppDataConfigurationController : IAppDataConfigurationController
    {
        public UniTask Load()
        {
            Application.targetFrameRate = 60;
            return UniTask.CompletedTask;
        }
    }
}