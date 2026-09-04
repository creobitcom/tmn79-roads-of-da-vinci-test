using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Data;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Controller
{
    public interface IVideoCutsceneController
    {
        /// <summary>Сейчас играет ролик.</summary>
        bool IsPlaying { get; }

        /// <summary>Последняя запущенная катсцена (нужна, чтобы понять, показывать ли комикс после).</summary>
        VideoCutsceneSO LastPlayed { get; }

        /// <summary>Все катсцены из библиотеки (для читов и отладки).</summary>
        IReadOnlyList<VideoCutsceneSO> Cutscenes { get; }

        /// <summary>Первый кадр готов и ролик пошёл — можно убирать затемнение перехода.</summary>
        event Action OnCutsceneStarted;

        /// <summary>Ролик доигран, пропущен или упал с ошибкой.</summary>
        event Action OnCutsceneCompleted;

        /// <summary>Вызывается мостом со сцены.</summary>
        void Initialize(CutsceneLibrarySO library, Transform uiRoot);

        /// <summary>Есть ли неигранная катсцена под одно из условий.</summary>
        bool TryGetReady(out VideoCutsceneSO cutscene, params VideoCutsceneTrigger[] triggers);

        /// <summary>Выполнены ли условия показа конкретной катсцены.</summary>
        bool IsReady(VideoCutsceneSO cutscene);

        /// <summary>Найти катсцену по имени ассета.</summary>
        VideoCutsceneSO Find(string id);

        /// <summary>Проиграть ролик целиком (с подготовкой, субтитрами и пропуском).</summary>
        UniTask Play(VideoCutsceneSO cutscene);
    }
}
