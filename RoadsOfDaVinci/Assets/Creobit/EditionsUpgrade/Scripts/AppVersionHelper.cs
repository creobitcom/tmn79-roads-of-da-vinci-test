using System;
using System.Collections.Generic;
using UnityEngine;


public class AppVersionHelper : MonoBehaviour
{
    private const string AppVersionKey = "AppVersion";
    [SerializeField] private List<GameObject> _blockedObjects = new List<GameObject>();
    [SerializeField] private bool _alwaysClearPrefs;
    private void Awake()
    {
#if MICROSOFT_GAME_CORE
        
        if (PlayerPrefs.HasKey(AppVersionKey) && !_alwaysClearPrefs)
        {
            SaveAppVersion();
        }
        else
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.SetString(AppVersionKey, Application.version);
        }
#endif

        foreach (var blockedObject in _blockedObjects)
        {
            blockedObject.SetActive(true);
        }
    }

    private void SaveAppVersion()
    {
        if (PlayerPrefs.GetString(AppVersionKey, String.Empty) != Application.version)
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.SetString(AppVersionKey, Application.version);
        }
    }
}