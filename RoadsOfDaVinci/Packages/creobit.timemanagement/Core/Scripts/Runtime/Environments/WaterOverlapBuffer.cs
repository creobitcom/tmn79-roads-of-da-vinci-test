using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Environments
{
    internal static class WaterOverlapBuffer
    {
        public const float DepthHalfExtent = 500f;

        private const int InitialCapacity = 32;
        private const int MaxCapacity = 512;
        private const int UnitCacheLimit = 256;

        private static Collider[] _hits = new Collider[InitialCapacity];
        private static readonly List<Collider> Result = new(InitialCapacity);
        private static readonly Dictionary<Collider, MovableObjectView> UnitCache = new();

        public static IReadOnlyList<Collider> Overlap(Vector3 center, Vector3 halfExtents, LayerMask layers)
        {
            Result.Clear();

            while (true)
            {
                var count = Physics.OverlapBoxNonAlloc(center, halfExtents, _hits, Quaternion.identity, layers,
                    QueryTriggerInteraction.Collide);

                if (count < _hits.Length || _hits.Length >= MaxCapacity)
                {
                    for (var i = 0; i < count; i++)
                    {
                        Result.Add(_hits[i]);
                    }

                    return Result;
                }

                _hits = new Collider[Mathf.Min(_hits.Length * 2, MaxCapacity)];
            }
        }

        public static MovableObjectView ResolveUnit(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            if (UnitCache.TryGetValue(collider, out var cached))
            {
                return cached;
            }

            if (UnitCache.Count >= UnitCacheLimit)
            {
                UnitCache.Clear();
            }

            var unit = collider.GetComponentInParent<MovableObjectView>();
            UnitCache[collider] = unit;
            return unit;
        }
    }
}
