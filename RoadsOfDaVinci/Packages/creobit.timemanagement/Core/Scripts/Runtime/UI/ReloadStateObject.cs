using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.UI
{
    public class ReloadStateObject : MonoBehaviour, IReloadable
    {
        [FormerlySerializedAs("isUI")] [SerializeField]
        private bool _isUI;
        
        [FormerlySerializedAs("rect")] [SerializeField]
        [ShowIf("_isUI")]
        private RectTransform _rect;
        
        [FormerlySerializedAs("startPos")] [SerializeField]
        private Vector3 _startPos;
        
        [SerializeField]
        private bool isEnable = true;
        
        [SerializeField]
        private Vector3 scale = Vector3.one;
        
        private IReloadController _reloadController;

        [Inject]
        public void Construct(IReloadController reloadController)
        {
            _reloadController = reloadController;
            
            _reloadController.AddReloadableObject(this);
        }

        [Button]
        public void SetPosition()
        {
            _startPos = _isUI
                ? _rect.anchoredPosition
                : transform.position;
        }

        public UniTask Reload()
        {
            if (_isUI)
                _rect.anchoredPosition = _startPos;
            else
                transform.position = _startPos;
            
            gameObject.SetActive(isEnable);
            transform.localScale = scale;
            
            return UniTask.CompletedTask;
        }
    }
}