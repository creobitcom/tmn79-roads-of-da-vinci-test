using System;
using UnityEngine;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene
{
    [Serializable]
    public struct MapSpotSpriteByState
    {
        public MapSpotState state;
        public MapSpotArtifactState artifactState;
        public Sprite sprite;
    }
}