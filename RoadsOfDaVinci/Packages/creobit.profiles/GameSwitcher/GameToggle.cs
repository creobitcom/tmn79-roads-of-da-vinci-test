using System.Linq;
using R3;
using UltEvents;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

public class GameToggle : MonoBehaviour
{
    [SerializeField] private SerializedDictionary<string, UltEvent> _GameCommands;
    [field: SerializeField] public UltEvent ActiveCommand { get; private set; }

    [SerializeField, Inject] private GameSwitcher _gameSwitcher;
    
    private async void Awake()
    {
        ChangeCommand(_gameSwitcher.CurrentGame.Value);
        _gameSwitcher.CurrentGame.Subscribe(ChangeCommand);
    }

    private void OnEnable()
    {
        ActivateCommand();
    }

    private void ChangeCommand(GameData gameData)
    {
        ActiveCommand = _GameCommands.First(x=>x.Key == gameData.GameName).Value;
    }

    public void ActivateCommand()
    {
        ActiveCommand.Invoke();
    }

    private void OnDestroy()
    {
        ActiveCommand.Clear();
    }
}