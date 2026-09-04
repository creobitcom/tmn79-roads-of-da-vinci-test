#if UNITY_STANDALONE
using System;
using System.Collections;
using System.Collections.Generic;
using TimeManagerEngine;
using UnityEngine;

public class ScreenResolutionController : MonoBehaviour
{
    [Header("Parameters")]
    [SerializeField]
    [Range(0.5f, 1f)]
    private float _checkChangesDelay = 1f;
    [Range(0.1f, 0.9f)]
    [SerializeField]
    private float _widthCorrectionPercent = 0.80f;

    private Resolution _maxResolution;
    private Resolution _lastResolution;

    private static ScreenResolutionController _instance;

    private void OnDestroy()
    {
#if TOYMAN
        Player.I.WindowModeActivated -= ActivateWindowMode;
#endif
        StopCoroutine(CheckResolutionChanges());
    }

    private void Awake()
    {
        if(_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        _lastResolution = GetCurrentResolution();

        _maxResolution = Screen.resolutions[Screen.resolutions.Length - 1];
#if TOYMAN
        Player.I.Fullscreen = Screen.fullScreen;
#endif
        DontDestroyOnLoad(gameObject);

        StartCoroutine(CheckResolutionChanges());
#if TOYMAN
        Player.I.WindowModeActivated += ActivateWindowMode;
#endif
    }

    private void ActivateWindowMode()
    {
        Resolution calculatedResolution = CalculateWindowModeResolution();

        Screen.SetResolution(calculatedResolution.width, calculatedResolution.height, false);
    }

    private IEnumerator CheckResolutionChanges()
    {
        while (true)
        {
            yield return new WaitForSeconds(_checkChangesDelay);     

            if (Screen.fullScreen)
            {
                continue;
            }

            Resolution currentResoultion = GetCurrentResolution();

            if (currentResoultion.width != _lastResolution.width
            || currentResoultion.height != _lastResolution.height)
            {
                ActivateWindowMode();

                _lastResolution = GetCurrentResolution();
            }
        }
    }

    private Resolution GetCurrentResolution()
    {
        return new Resolution()
        {
            width = Screen.width,
            height = Screen.height
        };
    }

    private Resolution CalculateWindowModeResolution()
    {
        int calculatedWidth = (int)Math.Round(_maxResolution.width * _widthCorrectionPercent);
        int calculatedHeight = (int)Math.Round(calculatedWidth / 1.77777777778f); // ���������� ������������� ������, ������� ����������� ��� ������� 16 �� 9

        return new Resolution()
        {
            width = calculatedWidth,
            height = calculatedHeight
        };
    }
}
#endif