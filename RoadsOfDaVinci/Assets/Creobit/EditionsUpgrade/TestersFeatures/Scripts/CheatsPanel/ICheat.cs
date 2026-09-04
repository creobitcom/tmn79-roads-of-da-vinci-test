
namespace Creobit.EditionsUpgrade
{
    public interface ICheat
    {
        string Title { get; }

        bool IsExecutable { get; }

        void Execute();
    }
}
