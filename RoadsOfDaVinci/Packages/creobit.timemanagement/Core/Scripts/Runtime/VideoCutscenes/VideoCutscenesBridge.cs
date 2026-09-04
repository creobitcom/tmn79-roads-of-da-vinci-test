using _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Data;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using UltEvents;
using UnityEngine;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes
{
    /// <summary>
    /// Мост между сценой и контроллером катсцен. Кладётся на Meta-сцену
    /// рядом с ComicsBridge. Хранит ссылку на библиотеку и корень, под который
    /// спавнится экран ролика.
    /// </summary>
    public class VideoCutscenesBridge : MonoBehaviour
    {
        [SerializeField] private CutsceneLibrarySO _library;

        [Tooltip("Канвас, под который спавнится экран катсцены. Пусто — спавнится под этим объектом.")]
        [SerializeField] private Transform _uiRoot;

        [SerializeField] private UltEvent _onCutsceneCompleted;

        private IVideoCutsceneController _controller;

        [Inject]
        private void Construct(IVideoCutsceneController controller)
        {
            _controller = controller;
        }

        private void Start()
        {
            if (_controller == null)
            {
                Log.Meta.Error("VideoCutscenesBridge: контроллер не проинжектился.");
                return;
            }

            if (_library == null)
            {
                Log.Meta.Warning("VideoCutscenesBridge: не задана библиотека катсцен.");
                return;
            }

            _controller.Initialize(_library, _uiRoot != null ? _uiRoot : transform);
            _controller.OnCutsceneCompleted += CutsceneCompletedHandler;
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.OnCutsceneCompleted -= CutsceneCompletedHandler;
            }
        }

        /// <summary>Ручной запуск по имени ассета — для UltEvent.</summary>
        public void PlayById(string id)
        {
            if (_controller == null)
            {
                return;
            }

            var cutscene = _controller.Find(id);

            if (cutscene == null)
            {
                Log.Meta.Error($"VideoCutscenesBridge: катсцена \"{id}\" не найдена в библиотеке.");
                return;
            }

            _controller.Play(cutscene).Forget();
        }

        /// <summary>Ручной запуск конкретного ассета — для UltEvent.</summary>
        public void Play(VideoCutsceneSO cutscene)
        {
            if (_controller == null || cutscene == null)
            {
                return;
            }

            _controller.Play(cutscene).Forget();
        }

        private void CutsceneCompletedHandler() => _onCutsceneCompleted?.InvokeSafe();
    }
}
