using TMPro;
using UltEvents;
using UnityEngine;
using VContainer;
using UnityEngine.UI;
using _8floor.TimeManagement.Core.Scripts.Runtime.ArtifactParts.Controller;
using Cysharp.Threading.Tasks;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.TasksController
{
    /*
   ✝

О Святой Отче наш, Творящий в скриптах и шейдерах,
Да святится имя Твое — void Start(),
Да приидет Царствие Твое — Update,
Да будет воля Твоя — public static void Main().

Хлеб наш насущный даждь нам днесь —
Инстанс нужный, и SerializeField,
И прости нам грехи наши — NullReferenceException,
Якоже и мы прощаем тем, кто git push без коммита.

Не введи нас во Warning (CS0168),
   Но избави нас от Infinite Loop.
   Ибо Твое есть Царство, и Сила, и Слава, и ФПС высокий во веки.

       Аминь. ✝
   */


    public class TasksStartView : MonoBehaviour
    {
        public Transform tasksParent;
        public TMP_Text levelNameText;
        public TMP_Text levelStoryText;
        public Image levelImage;

        [SerializeField]
        private UltEvent onCloseView;

        [SerializeField]
        private TMP_Text _artifactPartAvailableText;

        private IArtifactPartsController _artifactPartsController;
        private CanvasGroup _canvasGroup;

        [Inject]
        private void Construct(IArtifactPartsController artifactPartsController)
        {
            _artifactPartsController = artifactPartsController;
        }

        private void Awake()
        {
            TryGetComponent(out _canvasGroup);
        }

        private async void OnEnable()
        {
            // Окно переиспользуется между уровнями: возвращаем рейкасты, снятые в CloseView,
            // иначе при следующем показе оно не реагирует на клики.
            SetRaycastsBlocked(true);

            if (_artifactPartAvailableText != null)
            {
                await UniTask.WaitUntil(()=>_artifactPartsController.Service!=null);
                _artifactPartAvailableText.gameObject.SetActive(_artifactPartsController.Service.PartIsAvailable);
            }
        }

        public void CloseView()
        {
            // Окно живёт до конца Hide-анимации и его full-screen фон всё это время съедает клики
            // по элементам под ним (например, "нажмите, чтобы начать") — гасим рейкасты сразу.
            SetRaycastsBlocked(false);

            onCloseView?.Invoke();
        }

        private void SetRaycastsBlocked(bool blocked)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = blocked;
            }
        }
    }
}