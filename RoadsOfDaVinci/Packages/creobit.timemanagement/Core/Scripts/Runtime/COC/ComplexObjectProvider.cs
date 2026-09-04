using System;
using JetBrains.Annotations;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.COC
{
    public interface IComplexObjectProvider
    {
        public event Action<ComplexObject> CocAdded;
        
        public void AddCoc(ComplexObject complexObjectView);
    }

    [UsedImplicitly]
    public class ComplexObjectProvider : IComplexObjectProvider
    {
        public event Action<ComplexObject> CocAdded = delegate { };
        
        public void AddCoc(ComplexObject complexObjectView)
        {
            CocAdded?.Invoke(complexObjectView);
        }
    }
}