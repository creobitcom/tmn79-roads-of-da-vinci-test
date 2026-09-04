using Cysharp.Threading.Tasks;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload
{
    public interface IReloadable
    {
        public UniTask Reload();
    }
}
