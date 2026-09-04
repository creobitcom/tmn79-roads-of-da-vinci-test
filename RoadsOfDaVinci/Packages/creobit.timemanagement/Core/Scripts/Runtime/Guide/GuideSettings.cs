using System;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public class GuideSettings
{
    [field: SerializeField] public bool IncludeInGuides { get; private set; }

    [field: ShowIf(nameof(IncludeInGuides)), SerializeField, Range(1, 100)] public int Step { get; private set; } = 1;
    [field: ShowIf(nameof(IncludeInGuides)), SerializeField] public Vector2 Offset { get; private set; }
    [field: ShowIf(nameof(IncludeInGuides)), SerializeField] public bool UseLocalization { get; private set; }
    [field: ShowIf(nameof(_showLocalizationFieldCondition)), SerializeField] public string Key { get; private set; }
    [HideInInspector] public Vector3 Position;
    private bool _showLocalizationFieldCondition => IncludeInGuides && UseLocalization;
}
