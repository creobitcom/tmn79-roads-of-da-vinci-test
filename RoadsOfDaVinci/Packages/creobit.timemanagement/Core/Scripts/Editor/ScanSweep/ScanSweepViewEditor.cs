using UnityEditor;
using UnityEngine;
using _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.ScanSweep;

namespace _8floor.TimeManagement.Core.Scripts.Editor.ScanSweep
{
    [CustomEditor(typeof(ScanSweepView))]
    public class ScanSweepViewEditor : UnityEditor.Editor
    {
        private enum PreviewMode
        {
            None,
            Once3Sec,
            Loop
        }

        private const float HiddenProgress = 2f;
        private const float DefaultScanDuration = 3f;

        private PreviewMode _mode = PreviewMode.None;
        private double _lastTime;
        private float _totalScanElapsed;
        private float _passElapsed;
        private int _currentPass;
        private bool _inFlash;
        private float _flashElapsed;

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            StopPreview();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            EditorGUILayout.Space(12f);

            var defaultColor = GUI.backgroundColor;

            GUI.backgroundColor = _mode == PreviewMode.Once3Sec ? new Color(1f, 0.5f, 0.5f) : new Color(0.6f, 1f, 0.6f);
            var text3Sec = _mode == PreviewMode.Once3Sec ? "⏹ Остановить (3 сек)" : "▶ Посмотреть анимацию (3 сек + вспышка)";
            if (GUILayout.Button(text3Sec, GUILayout.Height(30f)))
            {
                if (_mode == PreviewMode.Once3Sec)
                {
                    StopPreview();
                }
                else
                {
                    StartPreview(PreviewMode.Once3Sec);
                }
            }

            EditorGUILayout.Space(2f);

            GUI.backgroundColor = _mode == PreviewMode.Loop ? new Color(1f, 0.5f, 0.5f) : new Color(0.6f, 0.9f, 1f);
            var textLoop = _mode == PreviewMode.Loop ? "⏹ Остановить зацикливание" : "🔁 Запустить анимацию (Бесконечно)";
            if (GUILayout.Button(textLoop, GUILayout.Height(30f)))
            {
                if (_mode == PreviewMode.Loop)
                {
                    StopPreview();
                }
                else
                {
                    StartPreview(PreviewMode.Loop);
                }
            }

            GUI.backgroundColor = defaultColor;

            serializedObject.ApplyModifiedProperties();
        }

        private void StartPreview(PreviewMode mode)
        {
            _mode = mode;
            _lastTime = EditorApplication.timeSinceStartup;
            _totalScanElapsed = 0f;
            _passElapsed = 0f;
            _currentPass = 0;
            _inFlash = false;
            _flashElapsed = 0f;
        }

        private void StopPreview()
        {
            _mode = PreviewMode.None;
            _inFlash = false;
            _flashElapsed = 0f;
            _totalScanElapsed = 0f;
            _passElapsed = 0f;
            _currentPass = 0;

            if (target is ScanSweepView view && view != null)
            {
                view.EditorStopPreview();
            }

            SceneView.RepaintAll();
        }

        private void OnEditorUpdate()
        {
            if (_mode == PreviewMode.None || target == null)
            {
                return;
            }

            var view = target as ScanSweepView;
            if (view == null)
            {
                StopPreview();
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var delta = (float)(now - _lastTime);
            _lastTime = now;

            if (delta <= 0f || delta > 0.5f)
            {
                return;
            }

            var settings = view.ActiveSweep;
            if (settings == null)
            {
                StopPreview();
                return;
            }

            var passDuration = settings.PassDuration;
            var passGap = settings.PassGap;
            var totalPassTime = passDuration + passGap;

            if (!_inFlash)
            {
                _totalScanElapsed += delta;
                _passElapsed += delta;

                if (_passElapsed < passDuration)
                {
                    var normalized = settings.EvaluatePass(Mathf.Clamp01(_passElapsed / passDuration));
                    var backwards = settings.PingPong && _currentPass % 2 == 1;
                    var progress = backwards ? 1f - normalized : normalized;

                    view.EditorPreviewStep(progress, 0f);
                }
                else if (_passElapsed < totalPassTime)
                {
                    view.EditorPreviewStep(HiddenProgress, 0f);
                }
                else
                {
                    _passElapsed = 0f;
                    _currentPass++;
                }

                if (_mode == PreviewMode.Once3Sec)
                {
                    var maxPasses = settings.LoopWhileInteracting ? int.MaxValue : settings.Passes;
                    var scanFinished = settings.LoopWhileInteracting
                        ? _totalScanElapsed >= DefaultScanDuration
                        : _currentPass >= maxPasses;

                    if (scanFinished)
                    {
                        if (settings.FlashOnFinish && settings.FlashDuration > 0f)
                        {
                            _inFlash = true;
                            _flashElapsed = 0f;
                        }
                        else
                        {
                            StopPreview();
                            return;
                        }
                    }
                }
                else if (_mode == PreviewMode.Loop)
                {
                    var maxPasses = settings.LoopWhileInteracting ? int.MaxValue : settings.Passes;
                    if (!settings.LoopWhileInteracting && _currentPass >= maxPasses)
                    {
                        if (settings.FlashOnFinish && settings.FlashDuration > 0f)
                        {
                            _inFlash = true;
                            _flashElapsed = 0f;
                        }
                        else
                        {
                            _currentPass = 0;
                            _totalScanElapsed = 0f;
                        }
                    }
                }
            }
            else
            {
                _flashElapsed += delta;
                var flashDuration = settings.FlashDuration;

                if (_flashElapsed < flashDuration)
                {
                    var envelope = settings.EvaluateFlash(_flashElapsed);
                    var flashAmount = settings.FlashIntensity * envelope;
                    var progress = settings.CutBandBeforeFlash ? HiddenProgress : 1f;

                    view.EditorPreviewStep(progress, flashAmount);
                }
                else
                {
                    if (_mode == PreviewMode.Loop)
                    {
                        _inFlash = false;
                        _flashElapsed = 0f;
                        _currentPass = 0;
                        _passElapsed = 0f;
                        _totalScanElapsed = 0f;
                    }
                    else
                    {
                        StopPreview();
                        return;
                    }
                }
            }

            SceneView.RepaintAll();
        }
    }
}
