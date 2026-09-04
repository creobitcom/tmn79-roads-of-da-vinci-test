using System.Collections.Generic;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Creobit.AddressablesController
{
    /// <summary>
    ///     The AddressablesController class is responsible for loading and unloading addressable assets in a Unity project.
    ///     It provides methods to load assets by reference or by name
    ///     and to unload assets either individually or all at once.
    /// </summary>
    public sealed class AddressablesController : IAddressablesController
    {
        private Dictionary<string, AsyncOperationHandle> _assetReferenceToHandleDictionary = new();
        private readonly Dictionary<string, int> _retainCountByAssetGuid = new();

        /// <summary>
        ///     Asynchronously loads an asset from an AssetReference. It will return an asset even if it already was loaded.
        /// </summary>
        /// <typeparam name="T">The type of the asset to be loaded.</typeparam>
        /// <param name="assetReference">The AssetReference to load the asset from.</param>
        /// <returns>
        ///     A UniTask representing the asynchronous loading operation,
        ///     with the asset of type T as the result.
        /// </returns>
        public async UniTask<T> LoadAssetByReferenceAsync<T>(AssetReference assetReference) where T : Object
        {
            var assetGuid = assetReference.AssetGUID;

            if (!_assetReferenceToHandleDictionary.TryGetValue(assetGuid, out var asyncOperationHandle))
            {
                asyncOperationHandle = assetReference.LoadAssetAsync<T>();
                _assetReferenceToHandleDictionary.Add(assetGuid, asyncOperationHandle);
            }

            _retainCountByAssetGuid.TryGetValue(assetGuid, out var retainCount);
            _retainCountByAssetGuid[assetGuid] = retainCount + 1;

            var isOperationInProgress = asyncOperationHandle.IsValid() && !asyncOperationHandle.IsDone;
            if (isOperationInProgress)
            {
                await asyncOperationHandle.Task.AsUniTask();
            }

            var isOperationSuccessful = asyncOperationHandle.IsValid() &&
                                        asyncOperationHandle.Status == AsyncOperationStatus.Succeeded;

            if (isOperationSuccessful)
            {
                return (T)asyncOperationHandle.Result;
            }

            ForgetFailedHandle(assetGuid, asyncOperationHandle);

            return null;
        }

        private void ForgetFailedHandle(string assetGuid, AsyncOperationHandle failedHandle)
        {
            if (_assetReferenceToHandleDictionary == null
                || !_assetReferenceToHandleDictionary.TryGetValue(assetGuid, out var currentHandle)
                || !currentHandle.Equals(failedHandle))
            {
                return;
            }

            _assetReferenceToHandleDictionary.Remove(assetGuid);
            _retainCountByAssetGuid.Remove(assetGuid);

            if (failedHandle.IsValid())
            {
                Addressables.Release(failedHandle);
            }
        }

        /// <summary>
        ///     Asynchronously loads an asset by its name.  It will return an asset even if it already was loaded.
        /// </summary>
        /// <typeparam name="T">The type of the asset to be loaded.</typeparam>
        /// <param name="key">The name key of the asset to be loaded.</param>
        /// <returns>
        ///     A UniTask representing the asynchronous loading operation,
        ///     with the asset of type T as the result.
        /// </returns>
        public UniTask<T> LoadAssetByNameAsync<T>(string key) where T : Object
        {
            return LoadAssetByReferenceAsync<T>(new AssetReference(key));
        }

        /// <summary>
        ///     Unloads an asset previously loaded using its AssetReference.
        /// </summary>
        /// <param name="assetRef">The AssetReference of the asset to be unloaded.</param>
        public void UnloadAssetReference(AssetReference assetRef)
        {
            if (assetRef == null)
            {
                Log.Bootstrap.Warning(
                    "[AddressablesService] UnloadAssetReference: trying to unload a null asset reference");
            }
            else if (_assetReferenceToHandleDictionary != null &&
                     _assetReferenceToHandleDictionary.TryGetValue(assetRef.AssetGUID, out var asyncOp))
            {
                if (_retainCountByAssetGuid.TryGetValue(assetRef.AssetGUID, out var retainCount) && retainCount > 1)
                {
                    _retainCountByAssetGuid[assetRef.AssetGUID] = retainCount - 1;
                    return;
                }

                _retainCountByAssetGuid.Remove(assetRef.AssetGUID);

                if (asyncOp.IsValid())
                {
                    Addressables.Release(asyncOp);
                }

                _assetReferenceToHandleDictionary.Remove(assetRef.AssetGUID);
            }
        }

        /// <summary>
        ///     Unloads all currently loaded asset references and releases their resources.
        /// </summary>
        public void UnloadAllAssetReferences()
        {
            if (_assetReferenceToHandleDictionary != null)
            {
                foreach (var keyValueAssetReference in _assetReferenceToHandleDictionary)
                    if (keyValueAssetReference.Value.IsValid())
                    {
                        Addressables.Release(keyValueAssetReference.Value);
                    }

                _assetReferenceToHandleDictionary.Clear();
                _retainCountByAssetGuid.Clear();
            }
        }

        public void Dispose()
        {
            UnloadAllAssetReferences();
            _assetReferenceToHandleDictionary = null;
        }
    }
}