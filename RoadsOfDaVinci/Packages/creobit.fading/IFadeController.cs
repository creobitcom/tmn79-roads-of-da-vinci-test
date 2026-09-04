using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Fading
{
    public interface IFadeController
    {

        public UniTask Init(GameObject fadeView);

        public UniTask FadeIn();

        public UniTask FadeOut();

        public UniTask FadeInOut();

    }
}
