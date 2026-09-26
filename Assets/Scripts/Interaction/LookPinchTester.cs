using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR;

namespace Toss.Interaction
{
    /// <summary>
    /// P0 Environment Validation: Pure XR Look-and-Pinch interaction test.
    /// Strictly requires real XR Eye Gaze tracking + real XR Hand Pinch tracking.
    /// NO mouse fallback, NO keyboard fallback, NO Camera.forward substitute, NO forced states.
    /// Logs real-time input events to Console and Logs/P0_RuntimeInputEvents.log.
    /// </summary>
    public class LookPinchTester : MonoBehaviour
    {
        [Header("State (Read-Only)")]
        [SerializeField] private bool _isGazed = false;
        [SerializeField] private bool _isPinched = false;
        [SerializeField] private bool _lookAndPinchTriggered = false;
        [SerializeField] private string _activeGazeSource = "None";
        [SerializeField] private string _activePinchSource = "None";
        [SerializeField] private float _currentPinchStrength = 0.0f;
        [SerializeField] private float _currentGazeConfidence = 0.0f;

        public bool IsGazed => _isGazed;
        public bool IsPinched => _isPinched;
        public bool LookAndPinchTriggered => _lookAndPinchTriggered;
        public string ActiveGazeSource => _activeGazeSource;
        public string ActivePinchSource => _activePinchSource;
        public float CurrentPinchStrength => _currentPinchStrength;
        public float CurrentGazeConfidence => _currentGazeConfidence;

        [Header("Event Counters")]
        public int gazeEnterCount = 0;
        public int gazeExitCount = 0;
        public int pinchStartCount = 0;
        public int pinchEndCount = 0;
        public int lookAndPinchTriggerCount = 0;

        [Header("XR References")]
        [SerializeField] private OVREyeGaze ovrEyeGaze;
        [SerializeField] private OVRHand leftHand;
        [SerializeField] private OVRHand rightHand;
        [SerializeField] private Renderer targetRenderer;

        private Vector3 _initialScale;
        private Material _defaultMaterial;
        private Material _gazeMaterial;
        private Material _pinchMaterial;
        private bool _wasPinched = false;
        private bool _wasGazed = false;
        private string _eventsLogFilePath;

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            _initialScale = transform.localScale;
            if (_initialScale == Vector3.zero)
            {
                _initialScale = new Vector3(0.15f, 0.15f, 0.15f);
                transform.localScale = _initialScale;
            }

            if (targetRenderer != null)
            {
                _defaultMaterial = targetRenderer.sharedMaterial;
            }

            // High-visibility materials for XR feedback
            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader");
            if (litShader != null)
            {
                _gazeMaterial = new Material(litShader) { color = new Color(1.0f, 0.85f, 0.1f) }; // Gold
                _pinchMaterial = new Material(litShader) { color = new Color(0.1f, 0.9f, 0.9f) }; // Cyan
            }

            string logsDir = Path.Combine(Application.dataPath, "..", "Logs");
            if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);
            _eventsLogFilePath = Path.Combine(logsDir, "P0_RuntimeInputEvents.log");
        }

        private void Start()
        {
            FindXRReferences();
            LogEvent("INIT", "LookPinchTester initialized on P0_InteractionTarget. Awaiting real XR inputs.");
        }

        private void Update()
        {
            FindXRReferences();
            UpdateGazeState();
            UpdatePinchState();
            UpdateVisualFeedback();
        }

        private void FindXRReferences()
        {
            if (ovrEyeGaze == null)
            {
                ovrEyeGaze = FindFirstObjectByType<OVREyeGaze>();
            }

            if (leftHand == null || rightHand == null)
            {
                var hands = FindObjectsByType<OVRHand>(FindObjectsSortMode.None);
                foreach (var h in hands)
                {
                    bool isLeft = h.GetHand() == OVRPlugin.Hand.HandLeft || h.name.Contains("Left");
                    bool isRight = h.GetHand() == OVRPlugin.Hand.HandRight || h.name.Contains("Right");

                    if (isLeft && leftHand == null) leftHand = h;
                    if (isRight && rightHand == null) rightHand = h;
                }
            }
        }

        private void UpdateGazeState()
        {
            bool hitThisTarget = false;
            string source = "None";
            float confidence = 0.0f;

            // 1. Meta Core SDK OVREyeGaze
            if (ovrEyeGaze != null && ovrEyeGaze.EyeTrackingEnabled)
            {
                confidence = ovrEyeGaze.Confidence;
                Ray gazeRay = new Ray(ovrEyeGaze.transform.position, ovrEyeGaze.transform.forward);
                if (Physics.Raycast(gazeRay, out RaycastHit hit, 10.0f))
                {
                    hitThisTarget = (hit.collider != null && hit.collider.gameObject == gameObject);
                }
                source = $"OVREyeGaze (Confidence: {confidence:F2})";
            }

            _isGazed = hitThisTarget;
            _activeGazeSource = source;
            _currentGazeConfidence = confidence;

            if (_isGazed != _wasGazed)
            {
                if (_isGazed)
                {
                    gazeEnterCount++;
                    LogEvent("GAZE_ENTER", $"Target entered by gaze from source [{source}]");
                }
                else
                {
                    gazeExitCount++;
                    LogEvent("GAZE_EXIT", $"Target exited by gaze");
                }
                _wasGazed = _isGazed;
            }
        }

        private void UpdatePinchState()
        {
            bool pinching = false;
            float maxStrength = 0.0f;
            string source = "None";

            // 1. Meta Core SDK OVRHand
            if (leftHand != null && leftHand.IsTracked)
            {
                bool leftPinch = leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
                float leftStrength = leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
                if (leftStrength > maxStrength) maxStrength = leftStrength;
                if (leftPinch)
                {
                    pinching = true;
                    source = $"OVRHand Left (Confidence: {leftHand.HandConfidence})";
                }
            }

            if (rightHand != null && rightHand.IsTracked)
            {
                bool rightPinch = rightHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
                float rightStrength = rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
                if (rightStrength > maxStrength) maxStrength = rightStrength;
                if (rightPinch)
                {
                    pinching = true;
                    source = $"OVRHand Right (Confidence: {rightHand.HandConfidence})";
                }
            }

            // 2. OpenXR Hand Tracking Devices (CommonUsages.trigger / pinch)
            if (!pinching)
            {
                var handDevices = new List<InputDevice>();
                InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HandTracking, handDevices);
                foreach (var dev in handDevices)
                {
                    if (dev.TryGetFeatureValue(CommonUsages.trigger, out float triggerVal))
                    {
                        if (triggerVal > maxStrength) maxStrength = triggerVal;
                        if (triggerVal > 0.5f)
                        {
                            pinching = true;
                            source = $"InputDevice ({dev.name}) HandTrigger";
                            break;
                        }
                    }
                }
            }

            _isPinched = pinching;
            _currentPinchStrength = maxStrength;
            _activePinchSource = source;

            if (_isPinched != _wasPinched)
            {
                if (_isPinched)
                {
                    pinchStartCount++;
                    LogEvent("PINCH_START", $"Real XR Hand pinch detected from source [{source}], strength: {maxStrength:F2}");
                }
                else
                {
                    pinchEndCount++;
                    LogEvent("PINCH_END", $"Real XR Hand pinch ended, max strength was: {maxStrength:F2}");
                }
                _wasPinched = _isPinched;
            }
        }

        private void UpdateVisualFeedback()
        {
            if (targetRenderer == null) return;

            if (_isGazed && _isPinched)
            {
                if (!_lookAndPinchTriggered)
                {
                    _lookAndPinchTriggered = true;
                    lookAndPinchTriggerCount++;
                    LogEvent("LOOK_AND_PINCH_TRIGGERED", $"Success! Real XR Gaze on target + real XR Hand Pinch active simultaneously. (Gaze: {_activeGazeSource}, Pinch: {_activePinchSource})");
                }

                if (_pinchMaterial != null) targetRenderer.material = _pinchMaterial;
                transform.localScale = _initialScale * 1.15f;
            }
            else if (_isGazed)
            {
                if (_gazeMaterial != null) targetRenderer.material = _gazeMaterial;
                transform.localScale = _initialScale * 1.05f;
            }
            else
            {
                if (_defaultMaterial != null) targetRenderer.material = _defaultMaterial;
                transform.localScale = _initialScale;
            }
        }

        private void LogEvent(string eventType, string details)
        {
            string logLine = $"[{DateTime.UtcNow:o}] [Frame {Time.frameCount}] [{eventType}] {details}";
            Debug.Log($"[P0 XR Event] {logLine}");

            try
            {
                if (!string.IsNullOrEmpty(_eventsLogFilePath))
                {
                    File.AppendAllText(_eventsLogFilePath, logLine + Environment.NewLine);
                }
            }
            catch
            {
                // Non-critical logging error handled silently
            }
        }
    }
}
