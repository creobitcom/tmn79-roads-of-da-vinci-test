using System.Diagnostics.CodeAnalysis;
using Creobit.Logger;
using R3;
using R3.Triggers;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects
{
    public class MovableObjectSortingLayerTrigger : MonoBehaviour
    {
        [SerializeField] private Collider _triggerCollider;
        [FormerlySerializedAs("_newUnitSortingLayerId")] [SerializeField] private int _newMovableObjectSortingLayerId;
        [FormerlySerializedAs("_unitTag")] [SerializeField] private string _movableObjectTag;
        
        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        private void Awake()
        {
            _triggerCollider ??= GetComponent<Collider>();
        }

        private void OnEnable()
        {
            _triggerCollider?.OnTriggerEnterAsObservable()
                .Where(other => other.gameObject.CompareTag(_movableObjectTag))
                .Subscribe(other =>
                {
                    if (other.TryGetComponent(out MovableObjectView unit))
                    {
                        unit.SortingGroup.sortingLayerID = _newMovableObjectSortingLayerId;
                    }
                    else
                    {
                        Log.Gameplay.Error("Can't get MovableObjectView component from unit object");
                    }
                }).AddTo(_disposables);
        }

        private void OnDisable()
        {
            _disposables.Clear();
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
        }
        
#if UNITY_EDITOR
        [Button, GUIColor("Green")]
        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        private void GetCollider()
        {
            _triggerCollider = GetComponent<Collider>();
        }
#endif
    }
}
