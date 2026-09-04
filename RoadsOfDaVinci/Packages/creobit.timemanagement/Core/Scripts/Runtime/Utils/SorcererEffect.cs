using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils
{
    public class SorcererEffect : MonoBehaviour
    {
        [SerializeField] private MovableObjectView _unitView;
        [SerializeField] private ParticleSystem _particlePrefab;
        [SerializeField] private Vector3 _spawnOffset;

        private ObjectPool<ParticleSystem> _particlePool;
        private ParticleSystem _currentActiveParticle;
        private CancellationTokenSource _lifetimeCts;

        private void Awake()
        {
            if (!_unitView) _unitView = GetComponentInParent<MovableObjectView>();

            _particlePool = new ObjectPool<ParticleSystem>(
                createFunc: CreateParticle,
                actionOnGet: ps => ps.gameObject.SetActive(true),
                actionOnRelease: ps =>
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.gameObject.SetActive(false);
                },
                actionOnDestroy: ps =>
                {
                    if (ps != null) Destroy(ps.gameObject);
                },
                collectionCheck: false,
                defaultCapacity: 5,
                maxSize: 20
            );
        }

        private void OnDestroy()
        {
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
        }

        private ParticleSystem CreateParticle() => Instantiate(_particlePrefab);

        public void SpawnCastEffect()
        {
            Vector3 spawnPosition;

            if (_unitView != null)
            {
                spawnPosition = _unitView.currentDestination + _spawnOffset;
            }
            else
            {
                spawnPosition = transform.position + _spawnOffset;
            }

            PlayEffectAsync(spawnPosition).Forget();
        }

        public void CastEffectEnded()
        {
            if (_currentActiveParticle == null) return;

            _lifetimeCts?.Cancel();

            if (_currentActiveParticle.gameObject.activeInHierarchy)
            {
                _particlePool.Release(_currentActiveParticle);
            }

            _currentActiveParticle = null;
        }

        private async UniTaskVoid PlayEffectAsync(Vector3 targetPos)
        {
            _lifetimeCts?.Cancel();
            _lifetimeCts = new CancellationTokenSource();

            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCts.Token,
                this.GetCancellationTokenOnDestroy()
            ).Token;

            var ps = _particlePool.Get();
            _currentActiveParticle = ps;

            ps.transform.position = targetPos;

            ps.Play(true);

            var waitTime = ps.main.duration + ps.main.startLifetime.constantMax;

            var isCanceled = await UniTask.Delay(
                TimeSpan.FromSeconds(waitTime),
                cancellationToken: linkedToken
            ).SuppressCancellationThrow();

            if (!isCanceled && ps != null && ps.gameObject.activeInHierarchy)
            {
                _particlePool.Release(ps);

                if (_currentActiveParticle == ps)
                {
                    _currentActiveParticle = null;
                }
            }
        }
    }
}