using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LogoController
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class LogoView : MonoBehaviour
    {
        [Required]
        [SerializeField]
        private SpriteRenderer _spriteRenderer;

        private ILogoController _logoController;
        private bool _subscribed;

        [Inject]
        private void Construct(ILogoController logoController)
        {
            _logoController = logoController;
            TrySubscribe();
        }

        private void Awake()
        {
            TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (_subscribed || _logoController == null || _spriteRenderer == null)
            {
                return;
            }

            _subscribed = true;
            _logoController.Logo
                .Subscribe(value => _spriteRenderer.sprite = value)
                .AddTo(this);
        }
    }
}
