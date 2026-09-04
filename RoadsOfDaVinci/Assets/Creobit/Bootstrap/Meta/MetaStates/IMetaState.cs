using Cysharp.Threading.Tasks;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Meta
{
    public interface IMetaState
    {
        public void StartState(MetaController metaController);

        public bool ChangeState(MetaController metaController, IMetaState newState);

        public UniTask EndState(MetaController metaController, bool async);
    }
}
