using System.Collections.Generic;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Reflection
{
    public sealed class BeamVisual
    {
        public readonly List<Vector3> Points = new();

        public LineRenderer Renderer;
    }
}