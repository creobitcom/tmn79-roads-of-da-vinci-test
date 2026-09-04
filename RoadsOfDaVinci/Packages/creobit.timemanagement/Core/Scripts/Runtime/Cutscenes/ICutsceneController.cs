using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Loading;
using Cysharp.Threading.Tasks;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes
{
    public interface ICutsceneController : ILoadUnit, IReloadable 
    {
        bool IsPlaying { get; }
        event Action CutsceneStarted;
        UniTask PlayCutsceneAsync(CutsceneSequenceSO sequenceSO, Action onComplete = null);
    }
}