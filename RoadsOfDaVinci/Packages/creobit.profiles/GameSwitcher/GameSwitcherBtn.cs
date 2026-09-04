using UltEvents;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

[RequireComponent(typeof(Button))]
public class GameSwitcherBtn : MonoBehaviour
{
    [SerializeField] private string _gameName;
    [SerializeField] private UltEvent _onClickEvent;
    [Inject] GameSwitcher _gameSwitcher;
    
    private Button _button;
   
    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(CallSwitch);
    }

    private void OnDestroy()
    {
        _button.onClick.RemoveListener(CallSwitch);
    }

    private void CallSwitch()
    {
        _gameSwitcher.SetCurrentGame(_gameName);   
        _onClickEvent?.Invoke();
    }
}