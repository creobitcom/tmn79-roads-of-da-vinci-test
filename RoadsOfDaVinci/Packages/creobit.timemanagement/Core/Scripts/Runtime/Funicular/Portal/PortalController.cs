using UnityEngine;
using System.Collections.Generic;
using Creobit.Audio;
using VContainer;

public class PortalController : MonoBehaviour
{
    private static readonly List<PortalController> _allPortals = new();

    public Transform[] points;

    [SerializeField] private PortalNode node;
    public float detectionRadius = 1.5f;
    
    private IAudioService _audioService;
    
    [Inject]
    public void Construct(IAudioService audioService)
    {
        _audioService = audioService;
    }
    
    private void OnEnable() => _allPortals.Add(this);
    private void OnDisable() => _allPortals.Remove(this);

    public static void CheckTeleport(Vector3 from, Vector3 to)
    {
        foreach (var p in _allPortals)
        {
            if (!p.enabled || p.points == null) continue;

            float sqrRad = p.detectionRadius * p.detectionRadius;

            foreach (var point in p.points)
            {
                if (point == null) continue;

                if (Vector3.SqrMagnitude(to - point.position) < sqrRad)
                {
                    if (p.node != null)
                    {
                        p.TeleportPerformed();
                    }
                    return;
                }
            }
        }
    }
    
    public void TeleportPerformed()
    {
        if (_audioService != null && node != null && node.PortalSound != null)
        {
            _audioService.PlaySfx(node.PortalSound);
        }

        if (node != null)
        {
            node.PlayEffect();
        }
    }
}