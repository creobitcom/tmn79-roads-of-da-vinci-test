using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Reflection.ReflectionController
{
    public class ReflectionController : IReflectionController
    {
        private readonly List<LaserEmitterView> _views = new();
        private CancellationTokenSource _cancellation = new();

        public UniTask Load()
        {
            TickAll().Forget();

            return UniTask.CompletedTask;
        }

        public void AddReflection(LaserEmitterView view)
        {
            _views.Add(view);
        }

        private async UniTask TickAll()
        {
            while (!_cancellation.Token.IsCancellationRequested)
            {
                foreach (var view in _views)
                {
                    Tick(view);
                }

                await UniTask.WaitForEndOfFrame(_cancellation.Token);
            }
        }
        
        private void Tick(LaserEmitterView view)
        {
            view.NewReceivers.Clear();
            ClearVisuals(view);
            
            if (!view.gameObject.activeInHierarchy)
            {
                return;
            }

            var origin = view.Origin.position;
            var direction = view.Origin.TransformDirection(view.LocalDirection).normalized;

            Trace(origin, direction, view);
            HandleExit(view);
            Draw(view);
        }
        
        private void ClearVisuals(LaserEmitterView view)
        {
            for (int i = 0; i < view.BeamVisuals.Count; i++)
            {
                var visual = view.BeamVisuals[i];

                visual.Points.Clear();

                visual.Renderer.gameObject.SetActive(false);

                view.RendererPool.Push(visual.Renderer);
            }

            view.BeamVisuals.Clear();
        }
        
        private BeamVisual CreateVisual(LaserEmitterView view)
        {
            LineRenderer renderer;

            if (view.RendererPool.Count > 0)
            {
                renderer = view.RendererPool.Pop();

                renderer.gameObject.SetActive(true);
            }
            else
            {
                renderer = Object.Instantiate(view.BeamRendererPrefab);
            }
            ApplyColor(renderer, view);
            var visual = new BeamVisual
            {
                Renderer = renderer
            };

            view.BeamVisuals.Add(visual);

            return visual;
        }

        private void Trace(Vector3 origin, Vector3 direction, LaserEmitterView view)
        {
            view.BeamStack.Clear();

            view.BeamStack.Push(new BeamData
            {
                Start = origin,
                Direction = direction.normalized,
                Depth = 0,
                Visual = CreateVisual(view)
            });

            while (view.BeamStack.Count > 0)
            {
                var beam = view.BeamStack.Pop();

                TraceSingleBeam(beam, view);
            }
        }

        private void TraceSingleBeam(BeamData beam, LaserEmitterView view)
        {
            if (beam.Depth >= view.maxReflections)
            {
                return;
            }
            
            beam.Visual.Points.Add(beam.Start);

            if (!Physics.Raycast(
                    beam.Start,
                    beam.Direction,
                    out var hit,
                    view.MaxDistance,
                    view.HitMask,
                    QueryTriggerInteraction.Collide))
            {
                var end = beam.Start + beam.Direction * view.MaxDistance;
                beam.Visual.Points.Add(end);

                return;
            }

            var hitPoint = hit.point + hit.normal * 0.002f;
            beam.Visual.Points.Add(hitPoint);

            HandleReceiver(hit, view);

            if (HandleTransparentReceiver(hit, beam, view))
            {
                return;
            }

            if (HandleSplitter(hit, beam, view))
            {
                return;
            }

            HandleReflection(hit, beam, view);
        }

        private bool HandleTransparentReceiver(RaycastHit hit, BeamData beam, LaserEmitterView view)
        {
            var receiver = hit.collider.GetComponentInParent<ILaserReceiver>();

            if (receiver == null || !receiver.IsTransparent)
            {
                return false;
            }

            view.BeamStack.Push(new BeamData
            {
                Start = hit.point + beam.Direction * view.surfaceOffset,
                Direction = beam.Direction,
                Depth = beam.Depth,
                Visual = beam.Visual
            });

            return true;
        }

        private bool HandleSplitter(RaycastHit hit, BeamData beam, LaserEmitterView view)
        {
            var splitter = hit.collider.GetComponentInParent<LaserSplitterView>();

            if (splitter == null)
            {
                return false;
            }

            var splitDirections = splitter.SplitDirections;

            for (var i = 0; i < splitDirections.Count; i++)
            {
                var splitData = splitDirections[i];

                var splitDirection = Quaternion.Euler(0f, 0f, splitData.AngleOffset) * splitter.transform.right;

                splitDirection.Normalize();

                view.BeamStack.Push(new BeamData
                {
                    Start = hit.point + splitDirection * view.surfaceOffset,
                    Direction = splitDirection,
                    Depth = beam.Depth + 1,
                    Visual = CreateVisual(view),
                });
            }

            return true;
        }

        private void HandleReflection(RaycastHit hit, BeamData beam, LaserEmitterView view)
        {
            var reflector = hit.collider.GetComponentInParent<ILaserReflector>();

            if (reflector == null)
            {
                return;
            }

            var reflectedDirection = reflector.Reflect(beam.Direction, hit.normal);

            view.BeamStack.Push(new BeamData
            {
                Start = hit.point + reflectedDirection * view.surfaceOffset,
                Direction = reflectedDirection,
                Visual = CreateVisual(view),
                Depth = beam.Depth + 1
            });
        }

        private void HandleReceiver(RaycastHit hit, LaserEmitterView view)
        {
            var receiver = hit.collider.GetComponentInParent<ILaserReceiver>();

            if (receiver == null)
            {
                return;
            }

            view.NewReceivers.Add(receiver);

            if (!view.CurrentReceivers.Contains(receiver))
            {
                receiver.OnLaserEnter();
            }
        }

        private void HandleExit(LaserEmitterView view)
        {
            foreach (var receiver in view.CurrentReceivers)
            {
                if (!view.NewReceivers.Contains(receiver))
                {
                    receiver.OnLaserExit();
                }
            }

            view.CurrentReceivers.Clear();

            foreach (var receiver in view.NewReceivers)
            {
                view.CurrentReceivers.Add(receiver);
            }
        }
        
        private void ApplyColor(LineRenderer renderer, LaserEmitterView view)
        {
            renderer.startColor = view.BeamColor;
            renderer.endColor = view.BeamColor;
        }

        private void Draw(LaserEmitterView view)
        {
            for (var i = 0; i < view.BeamVisuals.Count; i++)
            {
                var visual = view.BeamVisuals[i];
                var renderer = visual.Renderer;
                var pointCount = visual.Points.Count;

                renderer.positionCount = pointCount;

                for (var j = 0; j < pointCount; j++)
                {
                    renderer.SetPosition(j, visual.Points[j]);
                }
            }
        }

        public void Dispose()
        {
            _cancellation.Cancel();
        }
    }
}