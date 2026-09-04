using UnityEngine;

[RequireComponent(typeof(AITMNPath))]
public class UnitVisuals : MonoBehaviour
{
    private Renderer[] _renderers;
    private Canvas[] _canvases;
    private AITMNPath _ai;
    private Vector3 _lastPosition;
    private bool _isHidden = false;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _canvases = GetComponentsInChildren<Canvas>(true);
        _ai = GetComponent<AITMNPath>();
        _lastPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (_isHidden) return;

        if (Vector3.SqrMagnitude(_lastPosition - transform.position) > 9f)
        {
            Vector3 from = _lastPosition;
            Vector3 to = transform.position;

            if (CableRailway.Instance && CableRailway.Instance.isRestored && 
                CableRailway.Instance.TryRegisterLandedUnit(gameObject, to))
            {
                Toggle(false);
            }
            else
            {
                PortalController.CheckTeleport(from, to);
            }
        }

        _lastPosition = transform.position;
    }

    public void Realise()
    {
        if (_isHidden) Toggle(true);
    }

    private void Toggle(bool isVisible)
    {
        if (_isHidden == !isVisible) return;

        _ai.canMove = isVisible;
        _ai.isStopped = !isVisible;

        foreach (var r in _renderers) if (r != null) r.enabled = isVisible;
        foreach (var c in _canvases) if (c != null) c.enabled = isVisible;

        _isHidden = !isVisible;
        _lastPosition = transform.position;
    }
}