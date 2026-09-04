using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Creobit.UI.Utility
{
    public static class InstantiateExtensions
    {
        public static async UniTask<T> InstantiateAsync<T>(this T prefab,
            CancellationToken cancellationToken)
            where T : Object
        {
            return (await Object.InstantiateAsync(prefab).ToUniTask(cancellationToken: cancellationToken))[0];
        }
        
        public static async UniTask<T> InstantiateAsync<T>(this T prefab, Transform parent,
            CancellationToken cancellationToken)
            where T : Object
        {
            return (await Object.InstantiateAsync(prefab, parent).ToUniTask(cancellationToken: cancellationToken))[0];
        }
        
        public static async UniTask<T> InstantiateAsync<T>(this T prefab, Vector3 position,
            CancellationToken cancellationToken)
            where T : Object
        {
            return (await Object.InstantiateAsync(prefab, position, Quaternion.identity).ToUniTask(cancellationToken: cancellationToken))[0];
        }
        
        public static async UniTask<T> InstantiateAsync<T>(this T prefab, Transform parent, Vector3 position,
            CancellationToken cancellationToken)
            where T : Object
        {
            return (await Object.InstantiateAsync(prefab, parent, position, Quaternion.identity).ToUniTask(cancellationToken: cancellationToken))[0];
        }
    }
}