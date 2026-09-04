namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils
{
    public static class RuntimeConstants
    {
        public static class FoldoutNames
        {
            public const string Events = "Events";
            public const string Actions = "Actions";
            public const string Tags = "Tags";
            public const string Rotator = "Rotator";

            public const string ActivationConditions = "Activation Conditions";
            public const string Patrol = "Patrol";
            public const string ResourcesStealer = "Resources Stealer";
            public const string BuildingStates = "Building States";
            public const string Resources = "Resources";
            public const string InteractionInfo = "Interaction Settings";
            public const string ProductionInfo = "Production Settings";
            public const string MovableObjectInteractionInfo = "Movable Object Interaction Settings";
            public const string MovableObjectData = "Movable Object Settings";
            public const string TooltipSettings = "Tooltip Settings";
            public const string LevelTimerData = "Level Timer Data";
            public const string BaseData = "Base Settings";
            public const string GameplaySceneReferences = "Gameplay Scene References";
            public const string MapUIReferences = "Map UI References";
            public const string MenuUIReferences = "Menu UI References";
            public const string ComicsReferences = "Comics References";
            public const string CollectionRoomUIReferences = "CollectionRoom UI References";
            public const string GuidesUIReferences = "Guides UI References";
            public const string UIReferences = "UI References";
            public const string LevelTaskReferences = "Level Task References";
            public const string MovableObjectTaskSettings = "Movable Object Task Settings";
            public const string LevelSettings = "Level Settings";
            public const string InactiveObjectSettings = "Inactive Object Settings";
            public const string ComplexObjectControllerSettings = "COC Settings";
            public const string BuildingSettings = "Building Settings";
            public const string CocReferences = "COC References";
            public const string UniqueBadges = "Unique Badges";
            public const string PathSettings = "Path Settings";
            public const string AudioSettings = "Audio Settings";
            public const string Effects = "Effects";
            public const string Sprites = "Sprites";
            public const string BoostersReferences = "Boosters References";
            public const string Boosts = "Boosts";
            public const string Durations = "Durations";
            public const string UpgradeMark = "Upgrade Mark Settings";
            public const string DiseasableData = "Disease";
            public const string BreakableData = "Repair";
            public const string Guides = "Guide Settings";
            
            public const string Minigame = "Minigame";
            public const string Highlight = "Highlight";
        }

        public static class SpecialObjects
        {
            public const string FinishPointTag = "Finish";

            public const string RepairTag = "RepairTag";
            public const string HealTag = "HealTag";
        }

        public static class Intervals
        {
            public const int AccuracyMilliseconds = 10; // 0.01 seconds

            public const int AccuracySeconds = AccuracyMilliseconds / 100;
        }

        public static class Delays
        {
            public const int HoverDelay = 600;
            public const int ResourceAmountSpawn = 500;
        }

        public static class AnimationStates
        {
            public const string Idle = "Idle";
            public const string Run = "Run";
            public const string RunBag = "RunBag";
        }

        public static class Enums
        {
            public enum TaskProgress
            {
                Queued,
                InProgress,
                Cancelled,
                Completed,
                RunHome
            }
        }
    }
}