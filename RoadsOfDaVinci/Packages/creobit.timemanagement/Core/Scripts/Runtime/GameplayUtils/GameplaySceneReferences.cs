using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.BaseController;
using _8floor.TimeManagement.Core.Scripts.Runtime.Boosters;
using _8floor.TimeManagement.Core.Scripts.Runtime.CollectionItems;
using _8floor.TimeManagement.Core.Scripts.Runtime.Bubbles;
using _8floor.TimeManagement.Core.Scripts.Runtime.COC.UI;
using _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes.UI;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer.View;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController.View;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.MovableObjects.TaskBadges;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.UI;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial;
using Creobit.Dialogues.Core.Scripts.Runtime.View;
using Creobit.UI;
using Creobit.UI.Utility;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Rendering;
using RuntimeConstants = _8floor.TimeManagement.Core.Scripts.Runtime.Utils.RuntimeConstants;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils
{
    [FoldoutGroup(RuntimeConstants.FoldoutNames.GameplaySceneReferences)]
    [HideLabel]
    [Serializable]
    public class GameplaySceneReferences
    {
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public List<Canvas> GameplayCanvasLayers { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public Canvas MainCanvas { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public Canvas BubbleCanvas { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public Camera MainCamera { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.LevelTaskReferences)]
        [field: SerializeField]
        public Transform LevelTasksParent { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.LevelTaskReferences)]
        [field: AssetsOnly, SerializeField]
        public LevelTaskView LevelTaskView { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.LevelTaskReferences)]
        [field: AssetsOnly, SerializeField]
        public LevelTaskView LevelPveTaskView { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.CocReferences)]
        [field: SerializeField]
        public Transform CocTransform { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.CocReferences)]
        [field: SerializeField]
        public CocView CocView { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.CocReferences)]
        [field: SerializeField]
        public Transform ActionViewParent { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.CocReferences)]
        [field: SerializeField]
        public CocActionView[] ActionViews { get; private set; }

        [field: SerializeField]
        public CurrentLevelSO CurrentLevel { get; private set; }

#region DEBUG

        [field: SerializeField]
        public ResourcesViewRefs ResourcesView { get; private set; }


        [field: SerializeField]
        public InventoryResourcesViewRefs InventoryResourcesViewRefs { get; private set; }

#endregion
        // Плашка «получен предмет коллекции». Необязательная: части без коллекции оставляют пустой —
        // CollectionItemNotifier при null просто ничего не показывает.
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public CollectionNotificationView CollectionNotificationView { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: AssetsOnly, SerializeField]
        public TaskBadgeRefs TaskBadge { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: AssetsOnly, SerializeField]
        public TutorialArrow TutorialArrow { get; private set; }

        [field: SerializeField]
        public Light GlobalLight { get; private set; }

        [field: SerializeField]
        public Volume GlobalDarkVolume { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public TimerProgressView TimerProgressView { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public GameObject PressAnythingToContinueObject { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public GameObject FinishLevelObject { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public PanelReference WinView { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public PanelReference StartTasksView { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public PanelReference ConfirmTutorialView { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public PanelReference PauseView { get; private set; }


        [field: SerializeField]
        public LocalizedStringTable LocalizedStringTable { get; private set; }

        [field: SerializeField]
        public LoaderSceneReferences LoaderSceneReferences { get; private set; }

        [field: SerializeField]
        public GameplaySettings GameplaySettings { get; private set; }

        [field: SerializeField]
        public List<WorkerResourceByTag> WorkerResource { get; private set; }

        [field: SerializeField]
        public PathNode ShowPathNodePrefab { get; private set; }

        [field: SerializeField]
        public Transform PathNodesParent { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public ResourceAmountView ResourceAmountAdded { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public ResourceAmountView ResourceAmountSubtracted { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public NovelDialoguePanel[] DialoguePanels { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public CharacterDataSO[] CharactersDataSO { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public PanelReference GameplayTopPanel { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.BoostersReferences)]
        [field: SerializeField]
        public Transform BoostersParent { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.BoostersReferences)]
        [field: SerializeField]
        public BoosterView BoosterView { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InactiveObjectSettings)]
        [field: SerializeField]
        public MeshRenderer FogTexture { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InactiveObjectSettings)]
        [field: SerializeField]
        public Material FogMaterial { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.InactiveObjectSettings)]
        [field: SerializeField]
        public Material AntiFogMaterial { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public ObjectAlternativeSelectionView ObjectAlternativeSelectionView { get; private set; }
        
        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public CutscenePanelView CutscenePrefab { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        [field: Tooltip("Родитель (на весь экран, под UI Canvas), под которым спавнятся префабы комикс-сцен.")]
        public Transform CutsceneRoot { get; private set; }

        [field: FoldoutGroup("Craft")]
        [field: SerializeField]
        public CraftPanel CraftPanel { get; private set; }
        
        [field: FoldoutGroup("Craft")]
        [field: SerializeField]
        public PanelReference CraftView { get; private set; }

        [field: FoldoutGroup(RuntimeConstants.FoldoutNames.UIReferences)]
        [field: SerializeField]
        public BubbleView BubblePrefab { get; private set; }
    }
}