using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using _8floor.TimeManagement.Core.Scripts.Runtime.Ambience;
using _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes;
using _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController;
using _8floor.TimeManagement.Core.Scripts.Runtime.Loader;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Creobit.EditionsUpgrade;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Cysharp.Threading.Tasks;
using Pathfinding;
using UltEvents;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Cheats
{
    public static class GameplayCheats
    {
        private const float MaxSpeedMultiplier = 8f;

        private static readonly FieldInfo CutscenesOnLevelField = typeof(CutsceneBridge).GetField(
            "_cutscenesOnLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static float _unitsSpeedMultiplier = 1f;
        private static float _interactionSpeedMultiplier = 1f;
        private static int _speedStateSceneHandle = -1;
        private static int _unitsSpeedStateVersion;
        private static int _interactionSpeedStateVersion;

        private static int _cutsceneIndex;

        public static void GiveResources(int amountPerType)
        {
            if (!TryResolve<IGameResourcesSystem>(out var resources))
            {
                Debug.LogWarning("[Cheats] IGameResourcesSystem was not found.");
                return;
            }

            var targets = new HashSet<ResourceBaseSO>();

            foreach (var resource in resources.Resources.Keys)
            {
                targets.Add(resource);
            }

            if (TryResolve<ILevelLoader>(out var levelLoader)
                && levelLoader.LevelBaseSO.CurrentValue?.ResourcesForResourcePanel != null)
            {
                foreach (var resource in levelLoader.LevelBaseSO.CurrentValue.ResourcesForResourcePanel)
                {
                    if (resource != null)
                    {
                        targets.Add(resource);
                    }
                }
            }

            foreach (var resource in GameplayCheatsBridge.DefaultCheatResources)
            {
                if (resource != null)
                {
                    targets.Add(resource);
                }
            }

            if (targets.Count == 0)
            {
                Debug.LogWarning("[Cheats] No resources configured for cheat.");
                return;
            }

            foreach (var resource in targets)
            {
                resources.CheckResourceIsLoaded(resource);
                resources.AddResource(resource, amountPerType);
            }

            Debug.Log($"[Cheats] Added {amountPerType} to each resource " +
                      $"({targets.Count} types: {string.Join(", ", targets.Select(target => target.name))}).");
        }

        public static void CycleUnitsSpeed()
        {
            EnsureSpeedStateForActiveScene();
            _unitsSpeedMultiplier = NextMultiplier(_unitsSpeedMultiplier);
            ApplyUnitsSpeed(_unitsSpeedMultiplier);
        }

        public static void ResetUnitsSpeed()
        {
            EnsureSpeedStateForActiveScene();
            _unitsSpeedMultiplier = 1f;
            ApplyUnitsSpeed(_unitsSpeedMultiplier);
        }

        public static void CycleInteractionSpeed()
        {
            EnsureSpeedStateForActiveScene();
            _interactionSpeedMultiplier = NextMultiplier(_interactionSpeedMultiplier);
            ApplyInteractionSpeed(_interactionSpeedMultiplier);
        }

        public static void ResetInteractionSpeed()
        {
            EnsureSpeedStateForActiveScene();
            _interactionSpeedMultiplier = 1f;
            ApplyInteractionSpeed(_interactionSpeedMultiplier);
        }

        private static void ApplyUnitsSpeed(float multiplier)
        {
            var units = FindUnits();
            if (units.Length == 0)
            {
                Debug.LogWarning("[Cheats] Units were not found.");
                return;
            }

            int version = ++_unitsSpeedStateVersion;
            int applied = 0;
            foreach (var unit in units)
            {
                if (unit.WasLoaded)
                {
                    SetUnitSpeed(unit, multiplier);
                    applied++;
                }
                else
                {
                    ApplyUnitSpeedWhenLoaded(unit, multiplier, version).Forget();
                }
            }

            Debug.Log($"[Cheats] Units speed x{multiplier} ({applied} applied, {units.Length - applied} pending).");
        }

        private static async UniTaskVoid ApplyUnitSpeedWhenLoaded(
            MovableObjectView unit,
            float multiplier,
            int version)
        {
            await UniTask.WaitUntil(() => unit == null || unit.WasLoaded);
            if (unit == null || version != _unitsSpeedStateVersion)
            {
                return;
            }

            SetUnitSpeed(unit, multiplier);
        }

        private static void SetUnitSpeed(MovableObjectView unit, float multiplier)
        {
            unit.SetSpeed(unit.MovableObjectDataSO.Speed * multiplier);
        }

        private static void ApplyInteractionSpeed(float multiplier)
        {
            var units = FindUnits();
            if (units.Length == 0)
            {
                Debug.LogWarning("[Cheats] Units were not found.");
                return;
            }

            int version = ++_interactionSpeedStateVersion;
            int applied = 0;
            foreach (var unit in units)
            {
                if (unit.WasLoaded)
                {
                    SetUnitInteractionSpeed(unit, multiplier);
                    applied++;
                }
                else
                {
                    ApplyUnitInteractionSpeedWhenLoaded(unit, multiplier, version).Forget();
                }
            }

            Debug.Log($"[Cheats] Interaction speed x{multiplier} " +
                      $"({applied} applied, {units.Length - applied} pending).");
        }

        private static async UniTaskVoid ApplyUnitInteractionSpeedWhenLoaded(
            MovableObjectView unit,
            float multiplier,
            int version)
        {
            await UniTask.WaitUntil(() => unit == null || unit.WasLoaded);
            if (unit == null || version != _interactionSpeedStateVersion)
            {
                return;
            }

            SetUnitInteractionSpeed(unit, multiplier);
        }

        private static void SetUnitInteractionSpeed(MovableObjectView unit, float multiplier)
        {
            unit.SetBoosterInteractionSpeed(unit.MovableObjectDataSO.InteractionSpeed * multiplier);
        }

        private static MovableObjectView[] FindUnits()
        {
            return Object.FindObjectsByType<MovableObjectView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(unit => unit.gameObject.scene.IsValid() && !IsEnemy(unit))
                .ToArray();
        }

        private static bool IsEnemy(MovableObjectView unit)
        {
            if (unit.gameObject.name.ToLower().Contains("enemy"))
            {
                return true;
            }

            var dataName = unit.MovableObjectDataSO != null
                ? unit.MovableObjectDataSO.name.ToLower()
                : null;
            return dataName != null && dataName.Contains("enemy");
        }

        public static void CompleteAllTasks()
        {
            if (!TryResolve<ILevelTasksController>(out var tasksController))
            {
                Debug.LogWarning("[Cheats] ILevelTasksController was not found.");
                return;
            }

            int completed = 0;
            foreach (var pair in tasksController.LevelTasks.ToArray())
            {
                if (pair.Value.GetTaskData().TaskStatus == LevelTaskStatus.Done)
                {
                    continue;
                }

                tasksController.ChangeTaskStatus(pair.Key, LevelTaskStatus.Done);
                completed++;
            }

            Debug.Log($"[Cheats] Completed tasks: {completed}.");
        }

        public static void WalkThroughObjects()
        {
            var objects = Object.FindObjectsByType<StaticObjectView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int unblocked = 0;
            foreach (var staticObject in objects)
            {
                if (staticObject == null || !staticObject.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (staticObject.InteractionGraphNode == null || !staticObject.isBlocking)
                {
                    continue;
                }

                staticObject.UnBlockInteractionGraphNode();
                unblocked++;
            }

            if (AstarPath.active != null)
            {
                AstarPath.active.AddWorkItem(ctx =>
                {
                    AstarPath.active.data.GetNodes(node =>
                    {
                        node.Walkable = true;
                        node.Blocked = -1;
                        node.Penalty = 0;
                    });
                    ctx.QueueFloodFill();
                });
                AstarPath.active.FlushWorkItems();
            }
            else
            {
                Debug.LogWarning("[Cheats] AstarPath was not found.");
            }

            Debug.Log($"[Cheats] Walk through objects enabled ({unblocked} objects).");
        }

        public static void FinishLevel()
        {
            var winBridge = Object.FindFirstObjectByType<WinBridge>();
            if (winBridge == null)
            {
                Debug.LogWarning("[Cheats] WinBridge was not found in the scene.");
                return;
            }

            winBridge.ShowWinView();
        }

        public static void FinishLevelWithStars(int starsCount)
        {
            var winBridge = Object.FindFirstObjectByType<WinBridge>();
            if (winBridge == null)
            {
                Debug.LogWarning("[Cheats] WinBridge was not found in the scene.");
                return;
            }

            if (!TryResolve<ILevelTimer>(out var levelTimer))
            {
                Debug.LogWarning("[Cheats] ILevelTimer was not found.");
                return;
            }

            if (!TryResolve<ILevelLoader>(out var levelLoader) || levelLoader.LevelBaseSO.CurrentValue == null)
            {
                Debug.LogWarning("[Cheats] Level data was not loaded.");
                return;
            }

            var timerData = levelLoader.LevelBaseSO.CurrentValue.LevelTimerData;
            var starsData = timerData.LevelStarsData;

            if (starsData == null || starsData.Length == 0)
            {
                Debug.LogWarning("[Cheats] LevelStarsData is empty.");
                return;
            }

            if (TryResolve<IPlayerProfilesController>(out var profilesController)
                && profilesController.Service.CurrentProfile.GameMode == GameMode.Easy)
            {
                Debug.LogWarning($"[Cheats] Easy mode always gives {starsData.Length} stars.");
            }

            float duration = timerData.GameplayIntervalGeneralParameters.DurationSeconds;
            float targetTime = GetRemainingTimeForStars(starsData, duration, starsCount);
            float currentTime = levelTimer.EvaluateTimer().RemainingTime;

            int delta = Mathf.Clamp(Mathf.RoundToInt(targetTime - currentTime), short.MinValue, short.MaxValue);
            levelTimer.AffectLevelTimer((short)delta);

            var result = levelTimer.EvaluateTimer();

            Debug.Log($"[Cheats] Win with {result.NumberOfStars} stars (requested {starsCount}, " +
                      $"timer {result.RemainingTime}/{duration}).");

            winBridge.ShowWinView();
        }

        private static float GetRemainingTimeForStars(LevelStarData[] starsData, float duration, int starsCount)
        {
            var thresholds = starsData
                .Select(star => duration - star.TimeLeftMoreThan)
                .OrderBy(threshold => threshold)
                .ToArray();

            starsCount = Mathf.Clamp(starsCount, 0, thresholds.Length);

            if (starsCount == 0)
            {
                return Mathf.Max(0f, Mathf.Ceil(thresholds[0]) - 1f);
            }

            return Mathf.Clamp(Mathf.Ceil(thresholds[starsCount - 1]), 0f, duration);
        }

        public static void PlayCutscene()
        {
            if (!TryResolve<ICutsceneController>(out var cutsceneController))
            {
                Debug.LogWarning("[Cheats] ICutsceneController was not found.");
                return;
            }

            if (cutsceneController.IsPlaying)
            {
                Debug.Log("[Cheats] Cutscene is already playing.");
                return;
            }

            var cutscenes = FindCutscenesOnLevel();

            if (cutscenes.Count == 0)
            {
                Debug.LogWarning("[Cheats] CutsceneSequenceSO was not found on the level.");
                return;
            }

            if (_cutsceneIndex < 0 || _cutsceneIndex >= cutscenes.Count)
            {
                _cutsceneIndex = 0;
            }

            var sequence = cutscenes[_cutsceneIndex];
            var number = _cutsceneIndex + 1;
            _cutsceneIndex = (_cutsceneIndex + 1) % cutscenes.Count;

            Debug.Log($"[Cheats] Playing cutscene '{sequence.name}' ({number}/{cutscenes.Count} " +
                      $"on the level: {string.Join(", ", cutscenes.Select(cutscene => cutscene.name))}).");

            cutsceneController.PlayCutsceneAsync(sequence).Forget();
        }

        private static List<CutsceneSequenceSO> FindCutscenesOnLevel()
        {
            var found = new List<CutsceneSequenceSO>();
            var seen = new HashSet<CutsceneSequenceSO>();

            var bridges = Object.FindObjectsByType<CutsceneBridge>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var bridge in bridges)
            {
                if (bridge == null || !bridge.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (CutscenesOnLevelField?.GetValue(bridge) is List<CutsceneEventSetup> setups)
                {
                    foreach (var setup in setups)
                    {
                        if (IsValidCutscene(setup?.Cutscene) && seen.Add(setup.Cutscene))
                        {
                            found.Add(setup.Cutscene);
                        }
                    }
                }
            }

            var components = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var component in components)
            {
                if (component == null || !component.gameObject.scene.IsValid())
                {
                    continue;
                }

                CollectCutscenesFromComponent(component, seen, found);
            }

            found.Sort((first, second) => string.CompareOrdinal(first.name, second.name));
            return found;
        }

        private static void CollectCutscenesFromComponent(
            MonoBehaviour component,
            HashSet<CutsceneSequenceSO> seen,
            List<CutsceneSequenceSO> found)
        {
            if (component == null)
            {
                return;
            }

            try
            {
                var type = component.GetType();
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (var field in fields)
                {
                    object val = null;
                    try
                    {
                        val = field.GetValue(component);
                    }
                    catch
                    {
                        continue;
                    }

                    if (val == null)
                    {
                        continue;
                    }

                    ExtractCutscenesFromValue(val, seen, found, 0);
                }
            }
            catch
            {
            }
        }

        private static void ExtractCutscenesFromValue(
            object val,
            HashSet<CutsceneSequenceSO> seen,
            List<CutsceneSequenceSO> found,
            int depth)
        {
            if (val == null || depth > 3)
            {
                return;
            }

            if (val is Object unityObj && !unityObj)
            {
                return;
            }

            if (val is CutsceneSequenceSO seq)
            {
                if (IsValidCutscene(seq) && seen.Add(seq))
                {
                    found.Add(seq);
                }
                return;
            }

            if (val is UltEventBase ultEvent)
            {
                CollectCutscenesFromUltEvent(ultEvent, seen, found);
                return;
            }

            if (val is Transform)
            {
                return;
            }

            if (val is System.Collections.IEnumerable enumerable && val is not string)
            {
                try
                {
                    foreach (var item in enumerable)
                    {
                        ExtractCutscenesFromValue(item, seen, found, depth + 1);
                    }
                }
                catch
                {
                }
                return;
            }

            if (val is Component || val is GameObject)
            {
                return;
            }

            var type = val.GetType();
            if (type.IsClass && type.Namespace != null && (type.Namespace.StartsWith("_8floor") || type.Namespace.StartsWith("Creobit") || type.Namespace.StartsWith("UltEvents")))
            {
                try
                {
                    var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var field in fields)
                    {
                        object subVal = null;
                        try
                        {
                            subVal = field.GetValue(val);
                        }
                        catch
                        {
                            continue;
                        }

                        ExtractCutscenesFromValue(subVal, seen, found, depth + 1);
                    }
                }
                catch
                {
                }
            }
        }

        private static void CollectCutscenesFromUltEvent(
            UltEventBase ultEvent,
            HashSet<CutsceneSequenceSO> seen,
            List<CutsceneSequenceSO> found)
        {
            var calls = ultEvent.PersistentCallsList;
            if (calls == null)
            {
                return;
            }

            foreach (var call in calls)
            {
                if (call == null || call.PersistentArguments == null)
                {
                    continue;
                }

                foreach (var arg in call.PersistentArguments)
                {
                    if (arg != null && arg.Object is CutsceneSequenceSO sequence && IsValidCutscene(sequence) && seen.Add(sequence))
                    {
                        found.Add(sequence);
                    }
                }
            }
        }

        private static bool IsValidCutscene(CutsceneSequenceSO sequence)
        {
            if (sequence == null)
            {
                return false;
            }

            if (sequence.ComicPrefab != null)
            {
                return true;
            }

            return sequence.Frames != null && sequence.Frames.Count > 0;
        }

        private static float NextMultiplier(float currentMultiplier)
        {
            return Mathf.Min(MaxSpeedMultiplier, Mathf.Max(1f, currentMultiplier) * 2f);
        }

        private static void EnsureSpeedStateForActiveScene()
        {
            int activeSceneHandle = SceneManager.GetActiveScene().handle;
            if (_speedStateSceneHandle == activeSceneHandle)
            {
                return;
            }

            _speedStateSceneHandle = activeSceneHandle;
            _unitsSpeedMultiplier = 1f;
            _interactionSpeedMultiplier = 1f;
            _unitsSpeedStateVersion++;
            _interactionSpeedStateVersion++;
        }

        public static LevelAmbience FindAmbience()
        {
            return Object.FindFirstObjectByType<LevelAmbience>(FindObjectsInactive.Include);
        }

        public static bool IsAmbienceEnabled()
        {
            var ambience = FindAmbience();

            return ambience != null && ambience.enableAmbience;
        }

        public static void SetAmbienceEnabled(bool isEnabled)
        {
            var ambience = FindAmbience();

            if (ambience == null)
            {
                Debug.LogWarning("[Cheats] LevelAmbience was not found on the level.");
                return;
            }

            ambience.enableAmbience = isEnabled;

            Debug.Log($"[Cheats] Ambience {(isEnabled ? "enabled" : "disabled")} (preset {ambience.preset}).");
        }

        public static void SetLevelPassTimerEnabled(bool isEnabled)
        {
            var timerView = LevelPassTimerView.Instance;

            if (timerView == null)
            {
                Debug.LogWarning("[Cheats] LevelPassTimerView was not found.");
                return;
            }

            timerView.SetEnabledByCheat(isEnabled);

            Debug.Log($"[Cheats] Level pass timer {(isEnabled ? "enabled" : "disabled")}.");
        }

        private static bool TryResolve<T>(out T result)
        {
            foreach (var scope in Object.FindObjectsByType<LifetimeScope>(FindObjectsSortMode.None))
            {
                if (scope.Container != null && scope.Container.TryResolve(out result))
                {
                    return true;
                }
            }

            result = default;
            return false;
        }
    }
}
