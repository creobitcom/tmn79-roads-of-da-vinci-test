using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using Creobit.Loading;
using Cysharp.Threading.Tasks;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.COC
{
    public interface IComplexObjectController : IDisposable, ILoadUnit
    {
        public event Action<ComplexObject> ObjectViewAdded;
        public event Action<ComplexObject> OnStartBuilding;
        public event Action<ComplexObject> OnBuildObject;
        public event Action<ComplexObject> OnDestroyObject;
        public void AddObjectView(ComplexObject coc);
        public bool IsOnlyOneActionAvailable(ComplexObject coc, CocActionData actionData);
        public void UpdateUpgradeMarkView(ComplexObject coc);
        public UniTask UpdateProductionResource(ComplexObject coc,
            StaticObjectView currentObject, StaticObjectView newObject);
        public bool IsEnoughRelevantUnits(BuildingSettings settings, out List<StaticObjectView> relevantBases);
        public bool IsEnoughResources(ComplexObject coc, bool requireResources, out ResourceAmount[] resources);
        public CocActionData GetActionData(ComplexObject coc);
    }
}