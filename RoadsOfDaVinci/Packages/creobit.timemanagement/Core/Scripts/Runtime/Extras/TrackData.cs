using System;
using UnityEngine;

[Serializable]
public class TrackData
{
    [field: SerializeField] public string Name { get; set; }
    [field: SerializeField] public AudioClip MusicClip { get; set; }
}
