using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.BaseController.View
{
    /// <summary>
    /// Represents the view for managing the state of unit icons in the base.
    /// </summary>
    public class BaseUnitsStateView : MonoBehaviour
    {
        [SerializeField] private Image _unitIconPrefab;
        [SerializeField] private RectTransform _unitIconsContent;
        
        private int _freeUnits;

        private Sprite _unitInsideBaseSprite;
        private Sprite _unitOutsideBaseSprite;

        private ObjectPool<Image> _unitIconsPool;

        private BaseUnitsStatesController _unitsStatesController;

        [field: SerializeField]
        public StaticObjectView Basement { get; private set; }

        public RectTransform UnitIconsContent => _unitIconsContent;

        public List<Image> UnitIcons { get; private set; } = new();

        public int MaxUnits { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [field: SerializeField]
        private UltEvent _onBaseEmpty;

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [field: SerializeField]
        private UltEvent _onUnitEnter;

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [field: SerializeField]
        private UltEvent _onUnitExit;

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.Events)]
        [field: SerializeField]
        private UltEvent _onBaseFull;

        /// <summary>
        /// Dependency injection constructor.
        /// </summary>
        /// <param name="baseUnitsStatesController">Controller of views.</param>
        [Inject]
        private void Construct(IObjectViewController objectViewController)
        {
            _unitsStatesController = objectViewController.GetUnitBaseController().UnitsStatesController;

            _unitsStatesController.AddView(this);
        }

        private void OnDestroy()
        {
            if (_unitsStatesController != null)
            {
                _unitsStatesController.RemoveView(this);
            }
        }

        /// <summary>
        /// Initializes the view with the given sprites for units inside and outside the base.
        /// </summary>
        /// <param name="unitInsideBaseSprite">Sprite when unit is free.</param>
        /// <param name="unitOutsideBaseSprite">Sprite when unit is busy.</param>
        public void Initialize(Sprite unitInsideBaseSprite, Sprite unitOutsideBaseSprite)
        {
            _unitInsideBaseSprite = unitInsideBaseSprite;
            _unitOutsideBaseSprite = unitOutsideBaseSprite;

            if (_unitIconPrefab != null && _unitIconPrefab.gameObject.activeSelf)
            {
                _unitIconPrefab.gameObject.SetActive(false);
            }

            if (_unitIconsPool == null)
            {
                _unitIconsPool = new(CreateUnitIcon, OnUnitIconGetted, OnUnitImageReleased);
            }

        }

        public void Dispose()
        {
            _unitIconsPool.Dispose();
        }

        /// <summary>
        /// Changes the maximum number of workers by the given value.
        /// </summary>
        /// <param name="changeValue">The value to change the maximum number of workers by (Can be -1).</param>
        public void ChangeMaxWorkers(int changeValue)
        {
            SetMaxWorkers(MaxUnits + changeValue);
        }

        /// <summary>
        /// Set the maximum number of workers by the given value.
        /// </summary>
        /// <param name="changeValue">The value to set the maximum number of workers.</param>
        public void SetMaxWorkers(int changeValue)
        {
            int oldMaxUnits = MaxUnits;

            MaxUnits = (int)Mathf.Max(changeValue, 0f);

            _freeUnits = Mathf.Clamp(_freeUnits, 0, MaxUnits);

            if (MaxUnits > oldMaxUnits)
            {
                int needToCreate = MaxUnits - oldMaxUnits;

                for (int i = 0; i < needToCreate; i++)
                {
                    _unitIconsPool.Get(); // show new icon of unit
                }
            }
            else
            {
                int needToRelease = oldMaxUnits - MaxUnits;

                for (int i = 0; i < needToRelease && UnitIcons.Count > 0; i++)
                {
                    var lastIcon = UnitIcons[UnitIcons.Count - 1];

                    if (lastIcon == null)
                    {
                        UnitIcons.RemoveAt(UnitIcons.Count - 1);

                        continue;
                    }

                    // ObjectPool.ReleaseLast() picks the last *inactive* item, so here it either
                    // no-ops (nothing inactive yet) or throws "already released" — either way the
                    // icon stayed on screen. Release the last shown icon explicitly instead.
                    _unitIconsPool.Release(lastIcon); //hide last icon of unit
                }
            }
        }

        /// <summary>
        /// Sets the number of free workers to the given absolute value.
        /// </summary>
        /// <param name="value">The number of workers currently in the base.</param>
        public void SetFreeWorkers(int value)
        {
            ChangeFreeWorkers(Mathf.Clamp(value, 0, MaxUnits) - _freeUnits);
        }

        /// <summary>
        /// Changes the number of free workers by the given value.
        /// </summary>
        /// <param name="changeValue">The value to change the number of free workers by (Can be -1).</param>
        public void ChangeFreeWorkers(int changeValue)
        {
            _freeUnits = Mathf.Clamp(_freeUnits + changeValue, 0, MaxUnits);

            var unlockedIconsAmount = 0;

            // Never index past the icons that actually exist: an exception here would abort the
            // whole state-change chain and freeze the panel on a stale count.
            var iconsToPaint = Mathf.Min(MaxUnits, UnitIcons.Count);

            for (var i = 0; i < iconsToPaint; i++)
            {
                var unitIcon = UnitIcons[i];

                if (unlockedIconsAmount < _freeUnits)
                {
                    unitIcon.sprite = _unitInsideBaseSprite;
                    unlockedIconsAmount += 1;
                    continue;
                }

                unitIcon.sprite = _unitOutsideBaseSprite;
            }

            ChangeFreeWorkersHandler(changeValue);

        }

        public bool IsBaseEmpty() => _freeUnits == 0;

        private void ChangeFreeWorkersHandler(int changeValue)
        {
            if (_freeUnits == 0)
            {
                _onBaseEmpty?.Invoke();
            }

            if (_freeUnits == MaxUnits)
            {
                _onBaseFull?.Invoke();
            }

            if (changeValue > 0)
            {
                _onUnitEnter?.Invoke();
            }

            if (changeValue < 0)
            {
                _onUnitExit?.Invoke();
            }
        }

        /// <summary>
        /// Factory void for object pool.
        /// </summary>
        /// <returns>The created unit icon.</returns>
        private Image CreateUnitIcon()
        {
            return Instantiate(_unitIconPrefab, _unitIconsContent);
        }

        /// <summary>
        /// Handles the event when a unit icon is retrieved from the pool.
        /// </summary>
        /// <param name="icon">The retrieved unit icon.</param>
        private void OnUnitIconGetted(Image icon)
        {
            icon.gameObject.SetActive(true);

            UnitIcons.Add(icon);
        }

        /// <summary>
        /// Handles the event when a unit icon is released back to the pool.
        /// </summary>
        /// <param name="icon">The released unit icon.</param>
        private void OnUnitImageReleased(Image icon)
        {
            if (icon != null)
            {
                icon.gameObject.SetActive(false);
            }

            UnitIcons.Remove(icon);
        }
    }
}