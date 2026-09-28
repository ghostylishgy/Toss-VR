using System;
using System.IO;
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

        [Header("Mesh Source & Bas-Relief Mode")]
        [Tooltip("If assigned, uses this high-precision real geometry relief mesh instead of procedural mesh generation.")]
        [SerializeField] private Mesh customReliefMesh;

        [Tooltip("If true, falls back to legacy procedural mesh generator.")]
        [SerializeField] private bool useProceduralMesh = false;

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

        [Header("Manual View Capture (Hotkey: P)")]
        [SerializeField] private bool autoCaptureOnStart = false;
        private bool isCapturingViews = false;

        private void Awake()
        {
            EnsureComponents();
            EnsureReliefMesh();
            ApplyVisuals();
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                // Ensure coin rotates smoothly and continuously on Play start
                isPaused = false;
                if (inspectionMode == CoinInspectionMode.StaticPreset)
                {
                    inspectionMode = CoinInspectionMode.SlowPitchFlip360;
                }
            }
        }

        private void OnValidate()
        {
            EnsureComponents();
            EnsureReliefMesh();
            ApplyVisuals();
        }

        private void EnsureComponents()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        }

        private void EnsureReliefMesh()
        {
#if UNITY_EDITOR
            if (customReliefMesh == null && !useProceduralMesh)
            {
                var fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Meshes/Coin/TossCoin_Heads_Relief.fbx");
                if (fbx != null)
                {
                    var mf = fbx.GetComponentInChildren<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        customReliefMesh = mf.sharedMesh;
                    }
                }
            }
#endif
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                HandleKeyboardInput();
            }
            UpdateInspectionRotation();
        }

        private void HandleKeyboardInput()
        {
            if (!Application.isPlaying) return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            // Inspection hotkeys during Play mode
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                SetPreset(PresetAngle.FrontHeads0);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                SetPreset(PresetAngle.Angle45);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            {
                SetPreset(PresetAngle.EdgeSide90);
            }
            else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
            {
                SetPreset(PresetAngle.Angle135);
            }
            else if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame)
            {
                SetPreset(PresetAngle.BackTails180);
            }
            else if (keyboard.spaceKey.wasPressedThisFrame)
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
            else if (keyboard.mKey.wasPressedThisFrame)
            {
                int next = ((int)inspectionMode + 1) % 4;
                if (next == 0) next = 1; // Always cycle through continuous rotation modes (SlowPitchFlip360 -> SlowYawSpin360 -> TumbleOrbit)
                inspectionMode = (CoinInspectionMode)next;
                isPaused = false;
                Debug.Log($"[CoinPresenter] Switched Inspection Mode to: {inspectionMode}");
            }
            else if (keyboard.bKey.wasPressedThisFrame)
            {
                ToggleBackdrop();
            }
            else if (keyboard.leftBracketKey.wasPressedThisFrame)
            {
                config.turntableRotationSpeed = Mathf.Max(5f, config.turntableRotationSpeed - 10f);
            }
            else if (keyboard.rightBracketKey.wasPressedThisFrame)
            {
                config.turntableRotationSpeed = Mathf.Min(180f, config.turntableRotationSpeed + 10f);
            }
            else if (keyboard.pKey.wasPressedThisFrame || keyboard.digit0Key.wasPressedThisFrame)
            {
                StartCoroutine(CaptureValidationViewsCoroutine());
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
                    if (currentPreset == PresetAngle.FrontHeads0)
                    {
                        // True 0° front view: Heads relief face (+Z) looks directly at camera
                        transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    }
                    else if (currentPreset == PresetAngle.Angle45)
                    {
                        // 45° oblique view: 18° pitch, 140° yaw to catch specular highlights across facial contours
                        transform.localRotation = Quaternion.Euler(18f, 140f, 0f);
                    }
                    else
                    {
                        transform.localRotation = baseDisplayRot * Quaternion.Euler(currentRotationAngle, 0f, 0f);
                    }
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

            bool isRealGeometry = (!useProceduralMesh && customReliefMesh != null);

            // 1. Assign real geometry Mesh or fallback to procedural mesh
            if (meshFilter != null)
            {
                if (isRealGeometry)
                {
                    meshFilter.sharedMesh = customReliefMesh;
                }
                else
                {
                    var newMesh = CoinMeshGenerator.GenerateCoinMesh(config);
                    meshFilter.sharedMesh = newMesh;
                }
            }

            // 2. Configure Material (PBR URP Lit)
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

                if (isRealGeometry)
                {
                    // Real 3D bas-relief geometry: strictly disable and clear fake normal/height/AO maps
                    coinMaterial.SetTexture("_BumpMap", null);
                    coinMaterial.DisableKeyword("_NORMALMAP");
                    coinMaterial.SetTexture("_OcclusionMap", null);
                    coinMaterial.DisableKeyword("_OCCLUSIONMAP");
                }
                else
                {
                    // Legacy procedural fallback
                    if (normalMap == null || aoMap == null)
                    {
                        CoinTextureGenerator.GenerateTextures(config, out normalMap, out aoMap, 1024, 512);
                    }

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
                }

                // Explicitly disable any emission
                coinMaterial.DisableKeyword("_EMISSION");
                coinMaterial.SetColor("_EmissionColor", Color.black);
            }
        }

        public void SetCustomReliefMesh(Mesh mesh)
        {
            customReliefMesh = mesh;
            useProceduralMesh = false;
            ApplyVisuals();
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

        [ContextMenu("Capture Validation Views")]
        public void CaptureValidationViews()
        {
            StartCoroutine(CaptureValidationViewsCoroutine());
        }

        private System.Collections.IEnumerator CaptureValidationViewsCoroutine()
        {
            if (isCapturingViews) yield break;
            isCapturingViews = true;

            var prevMode = inspectionMode;
            var prevPaused = isPaused;
            var prevAngle = currentRotationAngle;

            // 1. Capture 0° Front View
            SetPreset(PresetAngle.FrontHeads0);
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.15f);
            yield return new WaitForEndOfFrame();
            CaptureViews("View_0deg_Front");

            yield return new WaitForSeconds(0.15f);

            // 2. Capture 45° Oblique View
            SetPreset(PresetAngle.Angle45);
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.15f);
            yield return new WaitForEndOfFrame();
            CaptureViews("View_45deg_Oblique");

            // Restore continuous turntable state so coin keeps rotating uninterrupted
            inspectionMode = (prevMode == CoinInspectionMode.StaticPreset) ? CoinInspectionMode.SlowPitchFlip360 : prevMode;
            isPaused = false;
            currentRotationAngle = prevAngle;
            isCapturingViews = false;

            Debug.Log("[CoinPresenter] Both 0° Front and 45° Oblique views captured. Turntable rotation resumed!");
        }

        private void CaptureViews(string baseName)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) mainCam = FindFirstObjectByType<Camera>();

            string assetDir = Path.Combine(Application.dataPath, "Textures", "Coin");
            Directory.CreateDirectory(assetDir);
            string artifactDir = @"C:\Users\steve\.gemini\antigravity\brain\0277133a-dfeb-4f06-a221-280321d0da00";

            if (mainCam != null)
            {
                RenderCameraToDisk(mainCam, Path.Combine(assetDir, $"VR_Camera_{baseName}.png"), artifactDir, $"VR_Camera_{baseName}.png");
            }

            // Also capture high-clarity inspection framing
            GameObject tempCamGo = new GameObject("TempInspectionCam");
            try
            {
                Camera inspectCam = tempCamGo.AddComponent<Camera>();
                if (mainCam != null) inspectCam.CopyFrom(mainCam);
                inspectCam.clearFlags = CameraClearFlags.SolidColor;
                inspectCam.backgroundColor = new Color(0.12f, 0.12f, 0.13f, 1f);
                inspectCam.fieldOfView = 20f;
                inspectCam.nearClipPlane = 0.01f;
                inspectCam.farClipPlane = 10f;

                Vector3 coinPos = transform.position;
                Vector3 camPos = coinPos + new Vector3(0f, 0f, -0.12f);
                inspectCam.transform.position = camPos;
                inspectCam.transform.LookAt(coinPos);

                RenderCameraToDisk(inspectCam, Path.Combine(assetDir, $"Unity_Play_{baseName}.png"), artifactDir, $"Unity_Play_{baseName}.png");
            }
            finally
            {
                DestroyImmediate(tempCamGo);
            }
        }

        private void RenderCameraToDisk(Camera cam, string assetPath, string artifactDir, string artifactFilename)
        {
            int res = 1024;
            RenderTexture rt = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 8;
            RenderTexture prevRt = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
            tex.Apply();

            cam.targetTexture = prevRt;
            RenderTexture.active = null;
            DestroyImmediate(rt);

            byte[] bytes = tex.EncodeToPNG();
            DestroyImmediate(tex);

            File.WriteAllBytes(assetPath, bytes);
            if (Directory.Exists(artifactDir))
            {
                File.WriteAllBytes(Path.Combine(artifactDir, artifactFilename), bytes);
            }
            Debug.Log($"[CoinPresenter] Saved view to: {assetPath}");
        }
    }
}
