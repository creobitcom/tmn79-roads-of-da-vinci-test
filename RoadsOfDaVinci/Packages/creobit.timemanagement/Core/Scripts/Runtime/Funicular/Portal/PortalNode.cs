using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using System;

public class PortalNode : MonoBehaviour
{
    [SerializeField] private PortalNode targetPortal;
    [SerializeField] private ParticleSystem particlePrefab;
    [SerializeField] private float arrivalCooldown = 0.5f;

    [SerializeField] private AudioClip _portalSound; 
    public AudioClip PortalSound => _portalSound; 
    
    private float _ignoreTriggersUntilTime = 0f;
    private ObjectPool<ParticleSystem> _localParticlePool;
    
    private void Awake()
    {
        _localParticlePool = new ObjectPool<ParticleSystem>(
            createFunc: CreateParticle,
            actionOnGet: ps => ps.gameObject.SetActive(true),
            actionOnRelease: ps =>
            {
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.gameObject.SetActive(false);
                }
            },
            actionOnDestroy: ps => { if (ps != null) Destroy(ps.gameObject); },
            collectionCheck: false,
            defaultCapacity: 5,
            maxSize: 20
        );
    }

    private ParticleSystem CreateParticle()
    {
        var ps = Instantiate(particlePrefab);
        ps.transform.SetParent(transform);
        return ps;
    }

    public void PlayEffect()
    {
        if (!enabled || Time.time < _ignoreTriggersUntilTime) return;
        targetPortal.SetArrivalCooldown(arrivalCooldown);
        PlayEffectAsync().Forget();
        targetPortal.PlayEffectAsync().Forget();
    }

    public void SetArrivalCooldown(float seconds) => _ignoreTriggersUntilTime = Time.time + seconds;

    public async UniTaskVoid PlayEffectAsync()
    {
        var ps = _localParticlePool.Get();
        ps.transform.position = transform.position;
        ps.Play(true);
        var waitTime = ps.main.duration + ps.main.startLifetime.constantMax;
        await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        if (ps != null) _localParticlePool.Release(ps);
    }
}