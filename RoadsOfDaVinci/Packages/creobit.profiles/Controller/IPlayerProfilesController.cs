using System;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Loading;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller
{
    public interface IPlayerProfilesController : ILoadUnit<PlayerProfilesRulesData>, IDisposable
    {
        public PlayerProfilesRulesData Rules { get; }
        
        public PlayerProfilesService Service { get; }
    }
}