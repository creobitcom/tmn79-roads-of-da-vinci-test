using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.BaseController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using Creobit.Logger;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects
{
    public class UnitAvailabilityController
    {
        private readonly Dictionary<StaticObjectView, int> _baseRestrictions = new();
        private readonly Dictionary<StaticObjectView, BaseData> _baseDataPerBase = new();

        /// <summary>
        /// Изменилось количество доступных юнитов на базе. От него зависит и «хватает рабочих»
        /// в тултипе, и серая/обычная иконка в панели выбора альтернативы, а раньше об этом
        /// изменении не узнавал никто: класс был чистым словарём без единого события,
        /// и UI оставался с картинкой, снятой в момент открытия.
        /// </summary>
        public event Action<StaticObjectView> AvailabilityChanged = delegate { };

        public void RegisterBase(StaticObjectView baseTransform, BaseData baseData)
        {
            _baseDataPerBase[baseTransform] = baseData;

            AvailabilityChanged(baseTransform);
        }

        public void AddRestriction(StaticObjectView baseTransform, int restrictedUnits)
        {
            if (!_baseRestrictions.TryAdd(baseTransform, restrictedUnits))
                _baseRestrictions[baseTransform] += restrictedUnits;

            AvailabilityChanged(baseTransform);
        }

        public void RemoveRestriction(StaticObjectView baseTransform, int restrictedUnits)
        {
            if (!_baseRestrictions.ContainsKey(baseTransform)) return;

            _baseRestrictions[baseTransform] = Mathf.Max(0, _baseRestrictions[baseTransform] - restrictedUnits);

            if (_baseRestrictions[baseTransform] == 0)
                _baseRestrictions.Remove(baseTransform);

            AvailabilityChanged(baseTransform);
        }

        public int GetAvailableUnitCount(StaticObjectView baseTransform)
        {
            if (!_baseDataPerBase.TryGetValue(baseTransform, out var baseData))
            {
                Log.Gameplay.Error($"UnitAvailabilityController - BaseData not found for {baseTransform.name}");
                return 0;
            }

            int maxUnits = baseData.MaxUnitCount;

            if (_baseRestrictions.TryGetValue(baseTransform, out var restrictedUnits))
            {
                maxUnits -= restrictedUnits;
            }

            return Mathf.Max(0, maxUnits);
        }

        public int GetRestrictedUnitCount(StaticObjectView baseTransform)
        {
            return _baseRestrictions.GetValueOrDefault(baseTransform, 0);
        }        
    }
}