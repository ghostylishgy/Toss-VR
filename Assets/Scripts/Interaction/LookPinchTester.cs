using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

namespace Toss.Interaction
{
    /// <summary>
    /// P0 Environment Validation: Look-and-Pinch primitive interaction test.
    /// Confirms that Gaze tracking (Camera forward ray) + Hand Pinch can interact with a spatial primitive.
    /// No game logic (no Toss, no coin, no RNG).
    /// </summary>
    public class LookPinchTester : MonoBehaviour
    {
        [Header("Visual Feedback")]
        [SerializeField] private Material defaultMaterial;
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private Renderer targetRenderer;

        [Header("State")]
        public bool isGazed;
        public bool isPinched;

        private Material cachedOriginalMat;
        private Material gazeMat;
        private Material pinchMat;
        private Vector3 initialScale = Vector3.zero;

        public Vector3 InitialScale => initialScale;

        public void EnsureInitialized()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            if (initialScale == Vector3.zero)
            {
                initialScale = transform.localScale;
                if (initialScale == Vector3.zero)
                {
                    initialScale = new Vector3(0.15f, 0.15f, 0.15f);
                    transform.localScale = initialScale;
                }
            }

            if (gazeMat == null)
            {
                var s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader");
                if (s != null)
                {
                    gazeMat = new Material(s);
                    gazeMat.color = new Color(1.0f, 0.85f, 0.2f); // Gold/Yellow on Gaze
                }
            }

            if (pinchMat == null)
            {
                var s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader");
                if (s != null)
                {
                    pinchMat = new Material(s);
                    pinchMat.color = new Color(0.1f, 0.9f, 0.9f); // Cyan on Pinch
                }
            }

            if (cachedOriginalMat == null && targetRenderer != null)
            {
                cachedOriginalMat = targetRenderer.sharedMaterial;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            CheckGaze();
            CheckPinch();
            UpdateVisuals();
        }

        public void CheckGaze()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 10.0f))
            {
                isGazed = (hit.collider.gameObject == gameObject);
            }
            else
            {
                isGazed = false;
            }
        }

        public void CheckPinch()
        {
            // Check XR Hand Devices for pinch / trigger gesture
            var hands = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HandTracking | InputDeviceCharacteristics.HeldInHand, hands);

            bool xrPinch = false;
            foreach (var hand in hands)
            {
                // Check common pinch / trigger feature
                if (hand.TryGetFeatureValue(CommonUsages.trigger, out float triggerVal) && triggerVal > 0.5f)
                {
                    xrPinch = true;
                    break;
                }
                if (hand.TryGetFeatureValue(CommonUsages.primaryButton, out bool buttonVal) && buttonVal)
                {
                    xrPinch = true;
                    break;
                }
            }

            // Fallback for editor simulator / mouse testing: Space or Left Mouse Click
            bool fallbackPinch = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);

            isPinched = xrPinch || fallbackPinch;
        }

        public void UpdateVisuals()
        {
            EnsureInitialized();
            if (targetRenderer == null) return;

            if (isGazed && isPinched)
            {
                if (pinchMat != null) targetRenderer.material = pinchMat;
                transform.localScale = initialScale * 1.15f;
            }
            else if (isGazed)
            {
                if (gazeMat != null) targetRenderer.material = gazeMat;
                transform.localScale = initialScale * 1.05f;
            }
            else
            {
                if (cachedOriginalMat != null) targetRenderer.material = cachedOriginalMat;
                transform.localScale = initialScale;
            }
        }
    }
}
