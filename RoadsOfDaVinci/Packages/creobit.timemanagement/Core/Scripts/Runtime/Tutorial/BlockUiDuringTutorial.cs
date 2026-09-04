using System;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial
{
    // World-space UI на уровне не на gameplay-канвасе — IsActionAvailable / blockHudInput его не режут.
    public class BlockUiDuringTutorial : MonoBehaviour
    {
        [SerializeField]
        private GraphicRaycaster raycaster;

        [SerializeField]
        private Selectable[] selectables;

        private IDisposable _subscription;

        [Inject]
        private void Construct(ITutorialController tutorialController)
        {
            if (raycaster == null)
            {
                TryGetComponent(out raycaster);
            }

            if (selectables == null || selectables.Length == 0)
            {
                selectables = GetComponentsInChildren<Selectable>(true);
            }

            _subscription?.Dispose();
            _subscription = tutorialController.isShowing.Subscribe(SetBlocked);
            SetBlocked(tutorialController.isShowing.Value);
        }

        private void SetBlocked(bool tutorialShowing)
        {
            if (raycaster != null)
            {
                raycaster.enabled = !tutorialShowing;
            }

            if (selectables == null)
            {
                return;
            }

            for (var i = 0; i < selectables.Length; i++)
            {
                if (selectables[i] != null)
                {
                    selectables[i].interactable = !tutorialShowing;
                }
            }
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}
