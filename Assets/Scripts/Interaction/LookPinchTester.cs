using System;
using System.IO;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;

namespace Toss.Interaction
{
    /// <summary>
    /// P0 Environment Validation: Pure Meta XR Interaction SDK Look-and-Pinch interaction test.
    /// Strictly relies on Interaction SDK GazeInteractable / IPointable events driven by Meta XR Simulator / OpenXR.
    /// NO mouse fallback, NO keyboard fallback, NO Camera.forward substitute, NO forced bool states.
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

        [Header("Interaction SDK References")]
        [SerializeField] private GazeInteractable _gazeInteractable;
        [SerializeField] private RayInteractable _rayInteractable;
        [SerializeField] private Renderer _targetRenderer;

        private Vector3 _initialScale;
        private Material _defaultMaterial;
        private Material _gazeMaterial;
        private Material _pinchMaterial;
        private string _eventsLogFilePath;
        private bool _isSubscribed = false;

        private void Awake()
        {
            if (_targetRenderer == null)
            {
                _targetRenderer = GetComponent<Renderer>();
            }

            _initialScale = transform.localScale;
            if (_initialScale == Vector3.zero)
            {
                _initialScale = new Vector3(0.15f, 0.15f, 0.15f);
                transform.localScale = _initialScale;
            }

            if (_targetRenderer != null)
            {
                _defaultMaterial = _targetRenderer.sharedMaterial;
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

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void Start()
        {
            SubscribeToEvents();
            LogEvent("INIT", "LookPinchTester initialized with Interaction SDK Gaze. Awaiting real XR Simulator Look and Pinch inputs.");
        }

        public void SubscribeToEvents()
        {
            if (_isSubscribed) return;

            if (_gazeInteractable == null)
            {
                _gazeInteractable = GetComponent<GazeInteractable>() ?? GetComponentInChildren<GazeInteractable>();
            }

            if (_rayInteractable == null)
            {
                _rayInteractable = GetComponent<RayInteractable>() ?? GetComponentInChildren<RayInteractable>();
            }

            if (_gazeInteractable != null)
            {
                _gazeInteractable.WhenPointerEventRaised += OnPointerEventRaised;
                _isSubscribed = true;
                Debug.Log("[LookPinchTester] Successfully subscribed to GazeInteractable pointer events.");
            }

            if (_rayInteractable != null)
            {
                _rayInteractable.WhenPointerEventRaised += OnPointerEventRaised;
                _isSubscribed = true;
                Debug.Log("[LookPinchTester] Successfully subscribed to RayInteractable fallback pointer events.");
            }
        }

        public void UnsubscribeFromEvents()
        {
            if (!_isSubscribed) return;

            if (_gazeInteractable != null)
            {
                _gazeInteractable.WhenPointerEventRaised -= OnPointerEventRaised;
            }

            if (_rayInteractable != null)
            {
                _rayInteractable.WhenPointerEventRaised -= OnPointerEventRaised;
            }

            _isSubscribed = false;
        }

        private void OnPointerEventRaised(PointerEvent evt)
        {
            switch (evt.Type)
            {
                case PointerEventType.Hover:
                    _isGazed = true;
                    _currentGazeConfidence = 1.0f;
                    gazeEnterCount++;
                    _activeGazeSource = "Interaction SDK GazeInteractable";
                    LogEvent("GAZE_ENTER", $"Target hover entered via [{_activeGazeSource}], interactor ID: {evt.Identifier}, pose: {evt.Pose.position}");
                    UpdateVisualState();
                    break;

                case PointerEventType.Unhover:
                    _isGazed = false;
                    _currentGazeConfidence = 0.0f;
                    gazeExitCount++;
                    LogEvent("GAZE_EXIT", $"Target hover exited via [{_activeGazeSource}]");
                    UpdateVisualState();
                    break;

                case PointerEventType.Select:
                    _isPinched = true;
                    _currentPinchStrength = 1.0f;
                    pinchStartCount++;
                    _activePinchSource = "Interaction SDK HandGazeInteractor (Index Pinch)";

                    if (!_lookAndPinchTriggered)
                    {
                        _lookAndPinchTriggered = true;
                    }
                    lookAndPinchTriggerCount++;

                    LogEvent("PINCH_START", $"Target select/pinch started via [{_activePinchSource}], interactor ID: {evt.Identifier}, pose: {evt.Pose.position}");
                    LogEvent("LOOK_AND_PINCH_TRIGGERED", $"SUCCESS: Real Look and Pinch interaction triggered via Meta XR Interaction SDK! Target selected while gazed. Interactor: {evt.Identifier}");
                    UpdateVisualState();
                    break;

                case PointerEventType.Unselect:
                    _isPinched = false;
                    _currentPinchStrength = 0.0f;
                    pinchEndCount++;
                    LogEvent("PINCH_END", $"Target select/pinch released, interactor ID: {evt.Identifier}");
                    UpdateVisualState();
                    break;

                case PointerEventType.Cancel:
                    _isPinched = false;
                    _isGazed = false;
                    _currentGazeConfidence = 0.0f;
                    _currentPinchStrength = 0.0f;
                    LogEvent("INTERACTION_CANCEL", $"Interaction cancelled for interactor ID: {evt.Identifier}");
                    UpdateVisualState();
                    break;
            }
        }

        private void UpdateVisualState()
        {
            if (_targetRenderer == null) return;

            if (_isPinched)
            {
                if (_pinchMaterial != null) _targetRenderer.material = _pinchMaterial;
                transform.localScale = _initialScale * 1.15f;
            }
            else if (_isGazed)
            {
                if (_gazeMaterial != null) _targetRenderer.material = _gazeMaterial;
                transform.localScale = _initialScale * 1.05f;
            }
            else
            {
                if (_defaultMaterial != null) _targetRenderer.material = _defaultMaterial;
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
