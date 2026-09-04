using Creobit.EditionsUpgrade;
using UnityEngine;

public class CheatsCanvasNavigationPanel : MonoBehaviour
{
    [Header("Componets")]
    [SerializeField]
    private GameObject _panel;
    [SerializeField]
    private CheatsPanel _cheatsPanel;

    private void OnDestroy()
    {
        _cheatsPanel.Open -= OnCheatsPanelOpen;
        _cheatsPanel.Close -= OnCheatsPanelClose;
    }

    private void Awake()
    {
        _cheatsPanel.Open += OnCheatsPanelOpen;
        _cheatsPanel.Close += OnCheatsPanelClose;
    }

    private void OnCheatsPanelClose()
    {
        _panel.SetActive(true);
    }

    private void OnCheatsPanelOpen()
    {
        _panel.SetActive(false);
    }
}
