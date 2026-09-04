using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Reflection.ReflectionController;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Reflection
{
    public class LaserEmitterView : Rotatable, ILevelLoadUnit
    {
        [field: SerializeField]
        public Transform Origin { get; private set; }

        [field: SerializeField]
        public float MaxDistance { get; private set; }

        [field: SerializeField]
        public LayerMask HitMask { get; private set; }

        [field: SerializeField]
        public Vector3 LocalDirection { get; private set; } = Vector3.right;
        
        [field: SerializeField]
        public LineRenderer BeamRendererPrefab { get; private set; }
        [field: SerializeField]
        public Color BeamColor { get; private set; } = Color.red;
        
        public readonly HashSet<ILaserReceiver> CurrentReceivers = new();
        public readonly HashSet<ILaserReceiver> NewReceivers = new();
        public readonly List<BeamVisual> BeamVisuals = new();
        public readonly Stack<LineRenderer> RendererPool = new();
        public int maxReflections;
        public float surfaceOffset = 0.01f;
        public readonly Stack<BeamData> BeamStack = new();
        
        [Inject]
        private IReflectionController _reflectionController;

        public UniTask Load()
        {
            _reflectionController.AddReflection(this);
            
            return UniTask.CompletedTask;
        }

        public UniTask Dispose()
        {
            return UniTask.CompletedTask;
        }
    }
}