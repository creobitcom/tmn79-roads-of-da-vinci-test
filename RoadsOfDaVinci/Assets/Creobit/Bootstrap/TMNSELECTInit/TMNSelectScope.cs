using Creobit.Bootstrap.Core.Scripts.Runtime.Initialization.BootstrapInit;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class TMNSelectScope : LifetimeScope
{

    
    protected override void Configure(IContainerBuilder builder)
    {
        var tmnSelectParent = Find<BootstrapScope>() as BootstrapScope;
        builder.RegisterInstance(tmnSelectParent?.Switcher);
        
        builder.RegisterEntryPoint<TMNSelectFlow>();
        DontDestroyOnLoad(this);
    }
}