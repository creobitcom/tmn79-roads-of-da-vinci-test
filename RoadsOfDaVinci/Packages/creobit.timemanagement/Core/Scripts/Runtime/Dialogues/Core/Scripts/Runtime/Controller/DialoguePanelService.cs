using System.Collections.Generic;
using Creobit.AddressablesController;
using Creobit.Dialogues.Core.Scripts.Runtime.View;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using VContainer;

namespace Creobit.Dialogues.Core.Scripts.Runtime.Controller
{
    public interface IDialoguePanelService : ILoadUnit
    {
        public IDialoguePanel GetDialoguePanel(string prefabId);
    }

    public class DialoguePanelService : IDialoguePanelService
    {
        private IAddressablesController _addressablesController;
        
        public Dictionary<string, IDialoguePanel> _dialoguePanels = new (); // TODO : Temporary public

        [Inject]
        private void Construct(IAddressablesController addressablesController)
        {
            _addressablesController = addressablesController;
        }

        public IDialoguePanel GetDialoguePanel(string prefabId)
        {
            return _dialoguePanels[prefabId];
        }

        public UniTask Load()
        {
            return UniTask.CompletedTask;
        }
    }
}