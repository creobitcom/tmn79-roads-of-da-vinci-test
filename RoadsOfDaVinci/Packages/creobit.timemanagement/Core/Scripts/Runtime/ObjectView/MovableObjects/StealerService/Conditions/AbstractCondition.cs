using System;
using UltEvents;

[Serializable]
public abstract class AbstractCondition : IDisposable
{
    public abstract void Initialize();
    public abstract bool Evaluate();
    public abstract void Dispose();
}