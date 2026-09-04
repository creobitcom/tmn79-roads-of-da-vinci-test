using System;
using _8floor.TimeManagement.Artifacts.Runtime.Data;
using _8floor.TimeManagement.Artifacts.Runtime.Service;
using _8floor.TimeManagement.Core.Scripts.Runtime.ArtifactParts.View;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Systems;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Cysharp.Threading.Tasks;
using R3;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ArtifactParts.Controller
{
    // Сцено-независимая часть логики артефакта: доступность части, факт сборки, сохранение
    // прогресса профиля. Не должна знать про ILevelController/интервалы/reload — иначе Meta-сцена
    // не сможет её заинжектить (ILevelController/IGameplayIntervalsController/IReloadController
    // зарегистрированы только в GameplayScope, их нет в MetaScope).
    public abstract class ArtifactPartsServiceBase : IDisposable
    {
        protected IArtifactsService _artifactsService;

        protected PlayerProfilesService _profilesService;

        public ArtifactPartObjectView ArtifactObject { get; protected set; }

        public bool PartIsAvailable
        {
            get
            {
                try
                {
                    string partName = ArtifactObject.Data.ArtifactPart.Name;

                    if (_artifactsService.PartIsReceived(partName))
                    {
                        return false;
                    }

                    var artifact = GetPartArtifact(partName);

                    if (artifact == null)
                    {
                        throw new ArgumentNullException(nameof(partName), $"No artifact found for part: {partName}");
                    }

                    return _artifactsService.ArtifactIsAvailable(artifact);
                }
                catch
                {
                    return false;
                }
            }
        }

        public event Action PartCollected;

        [Inject]
        protected virtual void Construct(IPlayerProfilesController profilesController, IArtifactsService artifactsService)
        {
            _profilesService = profilesController.Service;
            _artifactsService = artifactsService;
        }

        public virtual void SetArtifactObject(ArtifactPartObjectView artifactObject)
        {
            ArtifactObject = artifactObject;

            ArtifactObject.Hide(true);

            if (!PartIsAvailable)
            {
                return;
            }

            // set artifact then initialize service

            artifactObject.Initialize().Forget();

            OnAfterSetArtifactObject();
        }

        // Сценоспецифичное поведение после того, как часть стала доступна: геймплей вешает это на
        // reload-цикл и интервал показа; мета сразу подписывается на Interacted и показывает.
        protected abstract void OnAfterSetArtifactObject();

        public virtual void Dispose()
        {
            if (ArtifactObject != null)
            {
                ArtifactObject.Interacted -= OnPartInteraction;
            }
        }

        protected ArtifactDataSO GetPartArtifact(string partName)
        {
            foreach (var artifact in _artifactsService.Artifacts)
            {
                foreach (var part in artifact.Parts)
                {
                    if (part.Name == partName)
                    {
                        return artifact;
                    }
                }
            }
            return null;
        }

        protected virtual void OnPartInteraction()
        {
            _profilesService.CurrentProfile.ReceivedArtifactPartsNames.Add(ArtifactObject.Data.ArtifactPart.Name);

            ArtifactObject.Hide(false);

            PartCollected?.Invoke();
        }
    }

    // Геймплейная реализация: часть артефакта появляется/пропадает по игровым интервалам,
    // завязана на текущий уровень (ILevelController) и reload-цикл. Резолвится только внутри
    // GameplayScope — там же зарегистрированы ILevelController/IGameplayIntervalsController/IReloadController.
    public class ArtifactPartsService : ArtifactPartsServiceBase, IReloadable
    {
        private ushort _showPartIntervalID;

        private ushort _activePartIntervalID;

        private ReactiveProperty<float> _activePartRemainingSeconds = new(0);

        private IDisposable _levelIsStartedListener;

        private ILevelController _levelController;

        private IGameplayIntervalsController _intervalsSystem;

        private IReloadController _reloadController;

        public ReadOnlyReactiveProperty<float> ActivePartRemainingSeconds => _activePartRemainingSeconds;

        public event Action PartHidden;
        public event Action PartShowed;

        // Отдельный [Inject]-метод (не переопределяет базовый Construct) — гейплейные зависимости
        // добавляются поверх общих, а не вместо. Meta-сервис его не имеет и не наследует этот тип,
        // поэтому VContainer никогда не пытается их резолвить в MetaScope.
        [Inject]
        private void ConstructGameplay(ILevelController levelController, IGameplayIntervalsController intervalsSystem,
            IReloadController reloadController)
        {
            _levelController = levelController;
            _intervalsSystem = intervalsSystem;
            _reloadController = reloadController;
        }

        protected override void OnAfterSetArtifactObject()
        {
            _reloadController.AddReloadableObject(this);

            SubscribeToEvents();
        }

        public UniTask Reload()
        {
            _activePartRemainingSeconds.Value = 0;

            UnSubscribeFromEvents();

            SubscribeToEvents();

            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            _reloadController.RemoveReloadableObject(this);

            UnSubscribeFromEvents();

            base.Dispose();
        }

        private void SubscribeToEvents()
        {
            _levelIsStartedListener = _levelController.IsLevelStarted.Subscribe(OnLevelStartedValueChanged);
        }

        private void UnSubscribeFromEvents()
        {
            if (ArtifactObject != null)
            {
                ArtifactObject.Interacted -= OnPartInteraction;
            }

            if (_levelIsStartedListener != null)
            {
                _levelIsStartedListener.Dispose();
            }
        }

        private void ShowPart()
        {
            ArtifactObject.Interacted += OnPartInteraction;

            ArtifactObject.Show(false);

            PartShowed?.Invoke();
        }

        private void HidePart()
        {
            ArtifactObject.Interacted -= OnPartInteraction;

            ArtifactObject.Hide(false);

            PartHidden?.Invoke();
        }

        private void OnLevelStartedValueChanged(bool levelIsStarted)
        {
            if (!levelIsStarted)
            {
                return;
            }

            _levelIsStartedListener.Dispose();

            _showPartIntervalID = _intervalsSystem.StartInterval(new GameplayIntervalSpecificParameters(
                  null,
                  null,
                  null,
                  null,
                  null,
                  null,
                  OnShowPartIntervalCompleted,
                  null,
                  null,
                  null
              ), ArtifactObject.Data.ShowArtifactPartInterval);
        }

        protected override void OnPartInteraction()
        {
            _reloadController.RemoveReloadableObject(this);

            base.OnPartInteraction();

            _intervalsSystem.CancelInterval(_activePartIntervalID);
        }

        private void OnShowPartIntervalCompleted()
        {
            _intervalsSystem.CancelInterval(_showPartIntervalID);

            _activePartRemainingSeconds.Value = ArtifactObject.Data.ActiveArtifactPartInterval.DurationSeconds;

            _activePartIntervalID = _intervalsSystem.StartInterval(new GameplayIntervalSpecificParameters(
                null,
                null,
                null,
                null,
                OnActivePartIntervalTicked,
                null,
                OnActivePartIntervalCompleted,
                null,
                null,
                null
                ), ArtifactObject.Data.ActiveArtifactPartInterval);

            ShowPart();
        }

        private void OnActivePartIntervalTicked()
        {
            _activePartRemainingSeconds.Value -= 1;
        }

        private void OnActivePartIntervalCompleted()
        {
            HidePart();

            _intervalsSystem.CancelInterval(_activePartIntervalID);
        }
    }

    // Мета-реализация: часть артефакта видна и кликабельна сразу, без интервалов и без привязки
    // к уровню — только базовая доступность и сохранение прогресса. Никаких доп. [Inject]-методов:
    // использует общий ArtifactPartsServiceBase.Construct(IPlayerProfilesController, IArtifactsService).
    public class ArtifactPartsServiceMeta : ArtifactPartsServiceBase
    {
        protected override void OnAfterSetArtifactObject()
        {
            ArtifactObject.Interacted += OnPartInteraction;

            ArtifactObject.Show(false);
        }
    }
}
