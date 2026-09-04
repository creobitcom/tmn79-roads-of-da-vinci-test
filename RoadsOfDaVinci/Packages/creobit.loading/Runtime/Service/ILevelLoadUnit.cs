using Cysharp.Threading.Tasks;

namespace Creobit.Loading
{
    public interface ILevelLoadUnit
    {
        UniTask Load();
        UniTask Dispose();
    }
}