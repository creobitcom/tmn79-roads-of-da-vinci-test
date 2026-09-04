using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using Creobit.Localization;
using Creobit.UI;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles.MetaUI
{
    public class TrophyBookPanelView : PanelData
    {
        [Header("Left Page (Slots)")]
        public TrophySlotView[] Slots = new TrophySlotView[6];
        [SerializeField] private Sprite _defaultLockedSprite;

        [Header("Right Page (Text)")]
        [SerializeField] private TMP_Text _godNameText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private TMP_Text _categoryHeaderText;

        [Header("Tabs")]
        [SerializeField] private Button _artifactsTabButton;
        [SerializeField] private Button _trophiesTabButton;
        [SerializeField] private GameObject _artifactsTabSelectedVisual;
        [SerializeField] private GameObject _artifactsTabUnselectedVisual;
        [SerializeField] private GameObject _trophiesTabSelectedVisual;
        [SerializeField] private GameObject _trophiesTabUnselectedVisual;
        [SerializeField] private string _artifactsHeaderLocalizeKey = "artefacts_txt_header";
        [SerializeField] private string _trophiesHeaderLocalizeKey = "trophies_txt_header";

        [Header("Navigation")]
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _prevButton;

        private ICollectiblesService _collectiblesService;
        private CollectiblesDatabaseSO _database;
        private AllLevelsSO _allLevels;

        private CollectibleType _currentTab = CollectibleType.Artifact;
        private int _currentPageIndex = 0;

        [Inject]
        private void Construct(ICollectiblesService collectiblesService, CollectiblesDatabaseSO database,
            IObjectResolver resolver)
        {
            _collectiblesService = collectiblesService;
            _database = database;
            resolver.TryResolve(out _allLevels);
        }

        public override UniTask Load()
        {
            Observable
                .EveryValueChanged(gameObject, x => x.activeSelf)
                .Skip(1)
                .Subscribe(OnPanelActiveStateChanged)
                .AddTo(this);

            if (_nextButton != null) _nextButton.onClick.AddListener(HandleNextPage);
            if (_prevButton != null) _prevButton.onClick.AddListener(HandlePrevPage);

            if (_artifactsTabButton != null) _artifactsTabButton.onClick.AddListener(HandleArtifactsTab);
            if (_trophiesTabButton != null) _trophiesTabButton.onClick.AddListener(HandleTrophiesTab);

            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            if (_nextButton != null) _nextButton.onClick.RemoveListener(HandleNextPage);
            if (_prevButton != null) _prevButton.onClick.RemoveListener(HandlePrevPage);

            if (_artifactsTabButton != null) _artifactsTabButton.onClick.RemoveListener(HandleArtifactsTab);
            if (_trophiesTabButton != null) _trophiesTabButton.onClick.RemoveListener(HandleTrophiesTab);

            base.Dispose();
        }

        private void OnPanelActiveStateChanged(bool isActive)
        {
            if (!isActive) return;

            _currentTab = CollectibleType.Artifact;
            _currentPageIndex = 0;
            UpdateTabs();
            RenderPage();
        }

        private void HandleArtifactsTab()
        {
            if (_currentTab == CollectibleType.Artifact) return;

            _currentTab = CollectibleType.Artifact;
            _currentPageIndex = 0;
            UpdateTabs();
            RenderPage();
        }

        private void HandleTrophiesTab()
        {
            if (_currentTab == CollectibleType.Trophy) return;

            _currentTab = CollectibleType.Trophy;
            _currentPageIndex = 0;
            UpdateTabs();
            RenderPage();
        }

        private void UpdateTabs()
        {
            bool isArtifacts = _currentTab == CollectibleType.Artifact;
            bool isTrophies = _currentTab == CollectibleType.Trophy;

            if (_artifactsTabSelectedVisual != null)
            {
                _artifactsTabSelectedVisual.SetActive(isArtifacts);
            }
            if (_artifactsTabUnselectedVisual != null)
            {
                _artifactsTabUnselectedVisual.SetActive(!isArtifacts);
            }

            if (_trophiesTabSelectedVisual != null)
            {
                _trophiesTabSelectedVisual.SetActive(isTrophies);
            }
            if (_trophiesTabUnselectedVisual != null)
            {
                _trophiesTabUnselectedVisual.SetActive(!isTrophies);
            }

            if (_categoryHeaderText != null)
            {
                var key = isArtifacts ? _artifactsHeaderLocalizeKey : _trophiesHeaderLocalizeKey;
                _categoryHeaderText.text = LocalizationService.Instance.GetText(key);
            }
        }

        private CollectibleGroupSO[] GetCurrentGroups()
        {
            if (_database == null || _database.Groups == null || _database.Groups.Length == 0)
            {
                return System.Array.Empty<CollectibleGroupSO>();
            }

            if (_artifactsTabButton == null && _trophiesTabButton == null)
            {
                return _database.Groups;
            }

            return _database.GetGroups(_currentTab);
        }

        private System.Collections.Generic.List<CollectibleItemSO> GetAvailableItems(CollectibleGroupSO group)
        {
            var items = new System.Collections.Generic.List<CollectibleItemSO>();

            if (group == null || group.Items == null)
            {
                return items;
            }

            for (int i = 0; i < group.Items.Length; i++)
            {
                var item = group.Items[i];

                if (item == null)
                {
                    continue;
                }

                if (_allLevels != null && !_allLevels.IsCollectibleAvailable(item))
                {
                    continue;
                }

                items.Add(item);
            }

            return items;
        }

        private struct BookPage
        {
            public CollectibleGroupSO Group;
            public int StartIndex;
            public int Count;
        }

        private static readonly Vector2[] Layout4 =
        {
            new Vector2(-392, 95),
            new Vector2(-157, 95),
            new Vector2(-392, -155),
            new Vector2(-157, -155)
        };

        private static readonly Vector2[] Layout3 =
        {
            new Vector2(-392, 95),
            new Vector2(-157, 95),
            new Vector2(-275, -155)
        };

        private static readonly Vector2[] Layout2 =
        {
            new Vector2(-392, -30),
            new Vector2(-157, -30)
        };

        private System.Collections.Generic.List<BookPage> GetPages()
        {
            var list = new System.Collections.Generic.List<BookPage>();
            var groups = GetCurrentGroups();
            for (int g = 0; g < groups.Length; g++)
            {
                var group = groups[g];
                if (group == null) continue;

                int total = GetAvailableItems(group).Count;
                if (total == 0) continue;

                if (total <= 4)
                {
                    list.Add(new BookPage { Group = group, StartIndex = 0, Count = total });
                }
                else if (total == 5)
                {
                    list.Add(new BookPage { Group = group, StartIndex = 0, Count = 3 });
                    list.Add(new BookPage { Group = group, StartIndex = 3, Count = 2 });
                }
                else if (total == 6)
                {
                    list.Add(new BookPage { Group = group, StartIndex = 0, Count = 3 });
                    list.Add(new BookPage { Group = group, StartIndex = 3, Count = 3 });
                }
                else
                {
                    for (int i = 0; i < total; i += 3)
                    {
                        list.Add(new BookPage { Group = group, StartIndex = i, Count = Mathf.Min(3, total - i) });
                    }
                }
            }

            return list;
        }

        private void HandleNextPage()
        {
            var pages = GetPages();
            if (_currentPageIndex < pages.Count - 1)
            {
                _currentPageIndex++;
                RenderPage();
            }
        }

        private void HandlePrevPage()
        {
            if (_currentPageIndex > 0)
            {
                _currentPageIndex--;
                RenderPage();
            }
        }

        private void RenderPage()
        {
            var pages = GetPages();
            if (pages.Count == 0) return;

            if (_currentPageIndex >= pages.Count)
            {
                _currentPageIndex = 0;
            }

            var page = pages[_currentPageIndex];
            var group = page.Group;
            var items = GetAvailableItems(group);

            int collectedInGroup = 0;
            int totalInGroup = items.Count;

            for (int i = 0; i < totalInGroup; i++)
            {
                if (_collectiblesService != null && _collectiblesService.IsFullyCollected(items[i]))
                {
                    collectedInGroup++;
                }
            }

            Vector2[] currentLayout;
            if (page.Count == 4)
            {
                currentLayout = Layout4;
            }
            else if (page.Count == 3)
            {
                currentLayout = Layout3;
            }
            else if (page.Count == 2)
            {
                currentLayout = Layout2;
            }
            else
            {
                currentLayout = Layout4;
            }

            for (int i = 0; i < Slots.Length; i++)
            {
                var slot = Slots[i];
                if (slot == null) continue;

                if (i >= page.Count || page.StartIndex + i >= items.Count)
                {
                    slot.SetHidden();
                    continue;
                }

                var item = items[page.StartIndex + i];

                if (i < currentLayout.Length && slot.transform is RectTransform rt)
                {
                    rt.anchoredPosition = currentLayout[i];
                }

                bool isUnlocked = _collectiblesService != null && _collectiblesService.IsFullyCollected(item);
                if (isUnlocked)
                {
                    slot.SetItem(item.Icon);
                }
                else
                {
                    slot.SetLocked(_defaultLockedSprite);
                }
            }

            if (_godNameText != null && group != null)
            {
                _godNameText.text = LocalizationService.Instance.GetText(group.GroupNameLocalizeKey);
            }
            if (_descriptionText != null && group != null)
            {
                _descriptionText.text = LocalizationService.Instance.GetText(group.DescriptionLocalizeKey);
            }
            if (_progressText != null)
            {
                _progressText.text = $"{collectedInGroup}\\{totalInGroup}";
            }

            if (_prevButton != null) _prevButton.gameObject.SetActive(_currentPageIndex > 0);
            if (_nextButton != null) _nextButton.gameObject.SetActive(_currentPageIndex < pages.Count - 1);
        }
    }
}
