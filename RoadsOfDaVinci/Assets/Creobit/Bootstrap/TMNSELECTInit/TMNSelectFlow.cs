using System.Threading;
using Creobit.Bootstrap.Core.Scripts.Runtime.DTO;
using Creobit.Bootstrap.Core.Scripts.Runtime.Fading;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;

public class TMNSelectFlow : IAsyncStartable
{
    private GameSwitcher _gameSwitcher;
    private IObjectResolver _objectResolver;
    private ILoadingController _loadingController;
    private IFadeController _fadeController;
    private IPlayerPrefsSaveProvider _playerPrefsSaveProvider;
    private RuntimeData _runtimeData;
    
    public TMNSelectFlow(ILoadingController loadingController, 
        IFadeController fadeController,
        IObjectResolver objectResolver, 
        IPlayerPrefsSaveProvider prefsSaveProvider,
        RuntimeData runtimeData,
        GameSwitcher gameSwitcher
        )
    {
        _loadingController = loadingController;
        _fadeController = fadeController;
        _objectResolver = objectResolver;
        _gameSwitcher = gameSwitcher;
        _runtimeData = runtimeData;
        _playerPrefsSaveProvider = prefsSaveProvider;
    }
    
    public async UniTask StartAsync(CancellationToken cancellation = new CancellationToken())
    {
        _objectResolver.Inject(_gameSwitcher);

        await _loadingController.ExecuteAllLoadingTasks(false);
        
        _fadeController.FadeOut().Forget();
    }
}