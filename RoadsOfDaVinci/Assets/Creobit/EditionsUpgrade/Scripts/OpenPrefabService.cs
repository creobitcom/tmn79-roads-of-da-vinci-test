using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.EditionsUpgrade
{
    public class OpenPrefabService : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private GameObject _prefab;
        private bool _spawned = false;
        private void Awake()
        {
            if (_button)
            {
                _button.onClick.AddListener(OnClick);
            }
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClick);
            }
        }

        public void OnClick()
        {
            if (!_spawned)
            {
                _prefab.SetActive(false);
                var spawnedObj = Instantiate(_prefab);
                spawnedObj.transform.localScale = Vector3.one;
                _prefab = spawnedObj;
                _prefab.SetActive(true);
                _spawned = true;
            }
            else
            {
                _prefab.SetActive(true);
            }
        }
    }
}
