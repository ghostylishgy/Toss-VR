using System;
using UnityEngine;

namespace Toss.Coin
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class CoinPresenter : MonoBehaviour
    {
        public enum CoinInspectionMode
        {
            StaticPreset,
            SlowPitchFlip360,
            SlowYawSpin360,
            TumbleOrbit
        }

        public enum PresetAngle
        {
            FrontHeads0,
            Angle45,
            EdgeSide90,
            Angle135,
            BackTails180
        }

        [Header("Coin Configuration")]
        [SerializeField]
        public CoinVisualConfig config = new CoinVisualConfig();

        [Header("Inspection & Turntable Control")]
        public CoinInspectionMode inspectionMode = CoinInspectionMode.SlowPitchFlip360;
        public PresetAngle currentPreset = PresetAngle.FrontHeads0;

        [Tooltip("Pause turntable rotation in continuous modes.")]
        public bool isPaused = false;

        [Tooltip("Current continuous rotation angle (degrees).")]
        [SerializeField]
        private float currentRotationAngle = 0f;

        [Header("Runtime Visual References")]
        [SerializeField] private Material coinMaterial;
        [SerializeField] private Texture2D normalMap;
        [SerializeField] private Texture2D aoMap;
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;

        [Header("Contrast Backdrop Reference (Optional)")]
        [SerializeField] private GameObject backdropDark;
        [SerializeField] private GameObject backdropLight;
        private bool isLightBackdrop = false;

        private void Awake()
        {
            EnsureComponents();
            ApplyVisuals();
        }

        private void OnValidate()
        {
            EnsureComponents();
            ApplyVisuals();
        }

        private void EnsureComponents()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        }

        private void Update()
        {
            HandleKeyboardInput();
            UpdateInspectionRotation();
        }

        private void HandleKeyboardInput()
        {
            // Inspection hotkeys during Play mode or in Editor
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                SetPreset(PresetAngle.FrontHeads0);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                SetPreset(PresetAngle.Angle45);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                SetPreset(PresetAngle.EdgeSide90);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                SetPreset(PresetAngle.Angle135);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                SetPreset(PresetAngle.BackTails180);
            }
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                if (inspectionMode == CoinInspectionMode.StaticPreset)
                {
                    inspectionMode = CoinInspectionMode.SlowPitchFlip360;
                    isPaused = false;
                }
                else
                {
                    isPaused = !isPaused;
                }
            }
            else if (Input.GetKeyDown(KeyCode.M))
            {
                int next = ((int)inspectionMode + 1) % 4;
                inspectionMode = (CoinInspectionMode)next;
                Debug.Log($"[CoinPresenter] Switched Inspection Mode to: {inspectionMode}");
            }
            else if (Input.GetKeyDown(KeyCode.B))
            {
                ToggleBackdrop();
            }
            else if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                config.turntableRotationSpeed = Mathf.Max(5f, config.turntableRotationSpeed - 10f);
            }
            else if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                config.turntableRotationSpeed = Mathf.Min(180f, config.turntableRotationSpeed + 10f);
            }
        }

        public void SetPreset(PresetAngle angle)
        {
            currentPreset = angle;
            inspectionMode = CoinInspectionMode.StaticPreset;
            isPaused = true;

            switch (angle)
            {
                case PresetAngle.FrontHeads0:
                    currentRotationAngle = 0f;
                    break;
                case PresetAngle.Angle45:
                    currentRotationAngle = 45f;
                    break;
                case PresetAngle.EdgeSide90:
                    currentRotationAngle = 90f;
                    break;
                case PresetAngle.Angle135:
                    currentRotationAngle = 135f;
                    break;
                case PresetAngle.BackTails180:
                    currentRotationAngle = 180f;
                    break;
            }

            Debug.Log($"[CoinPresenter] Snap to Preset: {angle} ({currentRotationAngle}°)");
        }

        private void UpdateInspectionRotation()
        {
            if (Application.isPlaying && !isPaused && inspectionMode != CoinInspectionMode.StaticPreset)
            {
                float speed = config.turntableRotationSpeed;
                currentRotationAngle = (currentRotationAngle + speed * Time.deltaTime) % 360f;
            }

            Quaternion baseDisplayRot = Quaternion.Euler(config.defaultDisplayAngle);

            switch (inspectionMode)
            {
                case CoinInspectionMode.StaticPreset:
                    // Flip along local X-axis according to preset angle, facing camera
                    transform.localRotation = baseDisplayRot * Quaternion.Euler(currentRotationAngle, 0f, 0f);
                    break;

                case CoinInspectionMode.SlowPitchFlip360:
                    // Continuous slow flip: Face -> Edge -> Back -> Edge -> Face
                    transform.localRotation = baseDisplayRot * Quaternion.Euler(currentRotationAngle, 0f, 0f);
                    break;

                case CoinInspectionMode.SlowYawSpin360:
                    // Continuous slow horizontal coin spin around Y-axis
                    transform.localRotation = baseDisplayRot * Quaternion.Euler(0f, currentRotationAngle, 0f);
                    break;

                case CoinInspectionMode.TumbleOrbit:
                    // Elegant 3D compound orbit showing rim reflections and relief
                    float rollZ = Mathf.Sin(currentRotationAngle * Mathf.Deg2Rad) * 20f;
                    transform.localRotation = baseDisplayRot * Quaternion.Euler(currentRotationAngle, currentRotationAngle * 0.7f, rollZ);
                    break;
            }
        }

        public void ApplyVisuals()
        {
            EnsureComponents();
            if (config == null) config = new CoinVisualConfig();

            // 1. Generate or update procedural Mesh
            if (meshFilter != null)
            {
                var newMesh = CoinMeshGenerator.GenerateCoinMesh(config);
                meshFilter.sharedMesh = newMesh;
            }

            // 2. Generate or update Textures if missing
            if (normalMap == null || aoMap == null)
            {
                CoinTextureGenerator.GenerateTextures(config, out normalMap, out aoMap, 1024, 512);
            }

            // 3. Configure Material (PBR URP Lit)
            if (coinMaterial == null && meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                coinMaterial = meshRenderer.sharedMaterial;
            }

            if (coinMaterial == null)
            {
                var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                coinMaterial = new Material(litShader)
                {
                    name = "M_TossCoin_Presence"
                };
                if (meshRenderer != null) meshRenderer.sharedMaterial = coinMaterial;
            }

            if (coinMaterial != null)
            {
                // Silver metallic baseline (non-chrome, non-emissive)
                coinMaterial.SetColor("_BaseColor", config.metalTint);
                coinMaterial.SetFloat("_Metallic", config.metallic);
                coinMaterial.SetFloat("_Smoothness", config.FaceSmoothness);

                if (normalMap != null)
                {
                    coinMaterial.SetTexture("_BumpMap", normalMap);
                    coinMaterial.EnableKeyword("_NORMALMAP");
                    coinMaterial.SetFloat("_BumpScale", config.reliefStrength);
                }

                if (aoMap != null)
                {
                    coinMaterial.SetTexture("_OcclusionMap", aoMap);
                    coinMaterial.SetFloat("_OcclusionStrength", 1.0f);
                }

                // Explicitly disable any emission
                coinMaterial.DisableKeyword("_EMISSION");
                coinMaterial.SetColor("_EmissionColor", Color.black);
            }
        }

        public void ToggleBackdrop()
        {
            isLightBackdrop = !isLightBackdrop;
            if (backdropDark != null) backdropDark.SetActive(!isLightBackdrop);
            if (backdropLight != null) backdropLight.SetActive(isLightBackdrop);
            Debug.Log($"[CoinPresenter] Backdrop toggled: {(isLightBackdrop ? "Light Contrast" : "Dark Neutral")}");
        }

        public void SetBackdropReferences(GameObject dark, GameObject light)
        {
            backdropDark = dark;
            backdropLight = light;
        }

        public void SetMaterialAndTextures(Material mat, Texture2D norm, Texture2D ao)
        {
            coinMaterial = mat;
            normalMap = norm;
            aoMap = ao;
            if (meshRenderer != null) meshRenderer.sharedMaterial = mat;
        }
    }
}
