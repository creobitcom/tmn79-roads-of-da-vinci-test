#if TMN_Module
using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles;
using _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map;
using Creobit.Bootstrap.Core.Scripts.Runtime.Analytics;
using Creobit.Bootstrap.Core.Scripts.Runtime.Meta;
using UnityEngine;
using VContainer.Unity;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Initialization.MetaInit
{
    public class MetaAnalytics : IStartable, IDisposable
    {
        private const string AllTrophiesWonSentKey = "all_awards_unlocked";

        private readonly AnalyticsRecorder _analyticsRecorder;
        private readonly IMetaController _metaController;
        private readonly IMapController _mapController;
        private readonly ICollectiblesService _collectiblesService;
        private readonly CollectiblesDatabaseSO _collectiblesDatabase;

        public MetaAnalytics(AnalyticsRecorder analyticsRecorder,
            IMetaController metaController,
            IMapController mapController,
            ICollectiblesService collectiblesService,
            CollectiblesDatabaseSO collectiblesDatabase)
        {
            _analyticsRecorder = analyticsRecorder;
            _metaController = metaController;
            _mapController = mapController;
            _collectiblesService = collectiblesService;
            _collectiblesDatabase = collectiblesDatabase;
        }

        public void Start()
        {
            _mapController.OnMapOpened += MapOpenedHandler;
            _metaController.OnStateChanged += StateChangedHandler;
        }

        public void Dispose()
        {
            _mapController.OnMapOpened -= MapOpenedHandler;
            _metaController.OnStateChanged -= StateChangedHandler;
        }

        private void MapOpenedHandler()
        {
            _analyticsRecorder.MapOpened();
        }

        private void StateChangedHandler()
        {
            if (_metaController.CurrentStates.Count == 0 ||
                _metaController.CurrentStates.Peek() != MetaStates.CollectionRoom)
            {
                return;
            }

            _analyticsRecorder.TrophyPanelOpened();
            TryReportAllTrophiesWon();
        }

        private void TryReportAllTrophiesWon()
        {
            if (PlayerPrefs.GetInt(AllTrophiesWonSentKey, 0) > 0)
            {
                return;
            }

            var trophiesCount = 0;

            foreach (var group in _collectiblesDatabase.GetGroups(CollectibleType.Trophy))
            {
                foreach (var item in group.Items)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    if (!_collectiblesService.IsFullyCollected(item))
                    {
                        return;
                    }

                    trophiesCount++;
                }
            }

            if (trophiesCount == 0)
            {
                return;
            }

            PlayerPrefs.SetInt(AllTrophiesWonSentKey, 1);
            _analyticsRecorder.AllTrophiesWon();
        }
    }
}
#endif
