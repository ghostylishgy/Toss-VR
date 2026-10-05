using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Toss.Coin;

namespace Toss.Editor
{
    [InitializeOnLoad]
    public static class P1_CoinPresenceSetup
    {
        private const string ScenePath = "Assets/Scenes/P1_CoinPresence.unity";
        private const string MeshAssetPath = "Assets/Meshes/Coin/Mesh_TossCoin_Baseline.asset";
        private const string MatAssetPath = "Assets/Materials/Coin/M_TossCoin_Presence.mat";
        private const string NormalTexPath = "Assets/Textures/Coin/Coin_Normal.png";
        private const string AoTexPath = "Assets/Textures/Coin/Coin_AO.png";

        static P1_CoinPresenceSetup()
        {
            EditorApplication.update += RunOnceOnUpdate;
        }

        private static void RunOnceOnUpdate()
        {
            EditorApplication.update -= RunOnceOnUpdate;
            var args = Environment.GetCommandLineArgs();
            bool forceGenerate = Array.Exists(args, a => a == "-generateP1Presence");
            bool forceAudit = Array.Exists(args, a => a == "-auditP1Presence");

            if (forceGenerate)
            {
                Debug.Log("[P1 Legacy Setup] Forced generation flag detected. Running legacy setup...");
                ExecuteLegacySetup();
                Debug.Log("[P1 Legacy Setup] Batch generation completed successfully. Exiting editor.");
                EditorApplication.Exit(0);
            }
            else if (forceAudit)
            {
                AuditCoinPresenceBaseline();
                EditorApplication.Exit(0);
            }
        }

        [MenuItem("Toss/Legacy/P1 Setup Coin Presence (Disabled - P1.1 Frozen)")]
        public static void SetupCoinPresenceScene()
        {
            if (!EditorUtility.DisplayDialog("Warning: P1.1 Hero Coin Frozen",
                "Running Legacy Coin Presence Setup will overwrite the frozen Hero Coin (Heads/Tails bas-relief FBX and Satin Silver material) with the obsolete procedural baseline.\n\nAre you sure you want to proceed?",
                "Proceed Anyway", "Cancel (Recommended)"))
            {
                Debug.LogWarning("[P1 Legacy Setup] Operation cancelled to preserve frozen P1.1 Hero Coin assets.");
                return;
            }

            ExecuteLegacySetup();
        }

        private static void ExecuteLegacySetup()
        {
            Debug.Log("[P1.1 Setup] Building P1 Coin Presence Baseline Scene and Assets...");

            // 1. Ensure required asset directories exist
            EnsureDirectories();

            // 2. Generate and bake Texture Assets
            var config = new CoinVisualConfig();
            BakeTextures(config, out Texture2D normalTex, out Texture2D aoTex);

            // 3. Create or update Material Asset (PBR URP Lit)
            var coinMat = BakeMaterial(config, normalTex, aoTex);

            // 4. Generate and bake Mesh Asset
            var coinMesh = BakeMesh(config);

            // 5. Create or configure P1_CoinPresence scene
            BuildScene(config, coinMesh, coinMat, normalTex, aoTex);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[P1.1 Setup] P1 Coin Presence Baseline successfully created at: " + ScenePath);
            AuditCoinPresenceBaseline();
        }

        private static void EnsureDirectories()
        {
            string[] dirs = {
                "Assets/Scenes",
                "Assets/Materials/Coin",
                "Assets/Textures/Coin",
                "Assets/Meshes/Coin"
            };

            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
        }

        private static void BakeTextures(CoinVisualConfig config, out Texture2D normalTex, out Texture2D aoTex)
        {
            CoinTextureGenerator.GenerateTextures(config, out Texture2D tempNormal, out Texture2D tempAo, 1024, 512);

            byte[] normalBytes = tempNormal.EncodeToPNG();
            byte[] aoBytes = tempAo.EncodeToPNG();

            File.WriteAllBytes(NormalTexPath, normalBytes);
            File.WriteAllBytes(AoTexPath, aoBytes);

            AssetDatabase.ImportAsset(NormalTexPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(AoTexPath, ImportAssetOptions.ForceUpdate);

            // Configure Normal Map import settings
            var normalImporter = AssetImporter.GetAtPath(NormalTexPath) as TextureImporter;
            if (normalImporter != null)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.wrapMode = TextureWrapMode.Clamp;
                normalImporter.filterMode = FilterMode.Bilinear;
                normalImporter.SaveAndReimport();
            }

            // Configure AO import settings
            var aoImporter = AssetImporter.GetAtPath(AoTexPath) as TextureImporter;
            if (aoImporter != null)
            {
                aoImporter.textureType = TextureImporterType.Default;
                aoImporter.sRGBTexture = false;
                aoImporter.wrapMode = TextureWrapMode.Clamp;
                aoImporter.filterMode = FilterMode.Bilinear;
                aoImporter.SaveAndReimport();
            }

            normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalTexPath);
            aoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AoTexPath);
        }

        private static Material BakeMaterial(CoinVisualConfig config, Texture2D normalTex, Texture2D aoTex)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatAssetPath);
            if (mat == null)
            {
                var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(litShader);
                AssetDatabase.CreateAsset(mat, MatAssetPath);
            }

            // PBR Silver alloy parameters
            mat.SetColor("_BaseColor", config.metalTint);
            mat.SetFloat("_Metallic", config.metallic);
            mat.SetFloat("_Smoothness", config.FaceSmoothness);

            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.EnableKeyword("_NORMALMAP");
                mat.SetFloat("_BumpScale", config.reliefStrength);
            }

            if (aoTex != null)
            {
                mat.SetTexture("_OcclusionMap", aoTex);
                mat.SetFloat("_OcclusionStrength", 1.0f);
            }

            // Zero emission
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Mesh BakeMesh(CoinVisualConfig config)
        {
            var mesh = CoinMeshGenerator.GenerateCoinMesh(config);
            var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath);
            if (existingMesh != null)
            {
                existingMesh.Clear();
                EditorUtility.CopySerialized(mesh, existingMesh);
                EditorUtility.SetDirty(existingMesh);
                return existingMesh;
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, MeshAssetPath);
                return mesh;
            }
        }

        private static void BuildScene(CoinVisualConfig config, Mesh coinMesh, Material coinMat, Texture2D normalTex, Texture2D aoTex)
        {
            // Open or create new scene
            Scene scene;
            if (File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            // 1. OVRCameraRig matching P0 verified baseline
            var cameraRigGo = GameObject.Find("OVRCameraRig");
            if (cameraRigGo == null)
            {
                var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab");
                if (rigPrefab != null)
                {
                    cameraRigGo = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
                    cameraRigGo.name = "OVRCameraRig";
                }
                else
                {
                    cameraRigGo = new GameObject("OVRCameraRig");
                    cameraRigGo.AddComponent<OVRCameraRig>();
                }
            }

            cameraRigGo.transform.position = Vector3.zero;
            cameraRigGo.transform.rotation = Quaternion.identity;

            var ovrManager = cameraRigGo.GetComponent<OVRManager>();
            if (ovrManager == null) ovrManager = cameraRigGo.AddComponent<OVRManager>();
            ovrManager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;

            // Ensure CenterEyeAnchor is present and tagged MainCamera
            var centerEye = cameraRigGo.transform.Find("TrackingSpace/CenterEyeAnchor");
            if (centerEye != null)
            {
                var cam = centerEye.GetComponent<Camera>();
                if (cam == null) cam = centerEye.gameObject.AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 100f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.12f, 0.12f, 0.13f, 1f); // Neutral dark pass-through placeholder
            }
            else
            {
                // Fallback camera if running without XR device
                var mainCamGo = GameObject.FindWithTag("MainCamera");
                if (mainCamGo == null)
                {
                    mainCamGo = new GameObject("Main Camera");
                    var cam = mainCamGo.AddComponent<Camera>();
                    mainCamGo.tag = "MainCamera";
                    mainCamGo.transform.position = new Vector3(0f, 1.6f, 0f);
                    mainCamGo.transform.rotation = Quaternion.identity;
                    cam.nearClipPlane = 0.05f;
                    cam.farClipPlane = 100f;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.12f, 0.12f, 0.13f, 1f);
                }
            }

            // 2. Lighting Rig (Key Light + Fill Light + Soft Ambient)
            var keyLightGo = GameObject.Find("Key_DirectionalLight");
            if (keyLightGo == null)
            {
                keyLightGo = new GameObject("Key_DirectionalLight");
                var light = keyLightGo.AddComponent<Light>();
                light.type = LightType.Directional;
            }
            var keyLight = keyLightGo.GetComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1.0f, 0.98f, 0.95f); // 5600K natural warm-white
            keyLight.intensity = 1.15f;
            keyLight.shadows = LightShadows.Soft;
            keyLightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var fillLightGo = GameObject.Find("Fill_DirectionalLight");
            if (fillLightGo == null)
            {
                fillLightGo = new GameObject("Fill_DirectionalLight");
                var light = fillLightGo.AddComponent<Light>();
                light.type = LightType.Directional;
            }
            var fillLight = fillLightGo.GetComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.85f, 0.90f, 1.0f); // Soft cool fill
            fillLight.intensity = 0.45f;
            fillLight.shadows = LightShadows.None;
            fillLightGo.transform.rotation = Quaternion.Euler(-25f, 140f, 0f);

            // 3. Contrast Backdrops (Dark Neutral vs Light Contrast for inspection)
            var backdropRoot = GameObject.Find("Inspection_Backdrop");
            if (backdropRoot == null)
            {
                backdropRoot = new GameObject("Inspection_Backdrop");
                backdropRoot.transform.position = new Vector3(0f, 1.6f, 0.85f);
            }

            var darkQuad = backdropRoot.transform.Find("Backdrop_Dark")?.gameObject;
            if (darkQuad == null)
            {
                darkQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                darkQuad.name = "Backdrop_Dark";
                darkQuad.transform.SetParent(backdropRoot.transform, false);
                darkQuad.transform.localScale = new Vector3(1.2f, 0.8f, 1f);
                var col = darkQuad.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);

                var r = darkQuad.GetComponent<MeshRenderer>();
                var darkMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                darkMat.color = new Color(0.12f, 0.13f, 0.14f, 1f);
                r.sharedMaterial = darkMat;
            }
            darkQuad.SetActive(true);

            var lightQuad = backdropRoot.transform.Find("Backdrop_Light")?.gameObject;
            if (lightQuad == null)
            {
                lightQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                lightQuad.name = "Backdrop_Light";
                lightQuad.transform.SetParent(backdropRoot.transform, false);
                lightQuad.transform.localScale = new Vector3(1.2f, 0.8f, 1f);
                var col = lightQuad.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);

                var r = lightQuad.GetComponent<MeshRenderer>();
                var lightMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                lightMat.color = new Color(0.78f, 0.79f, 0.82f, 1f);
                r.sharedMaterial = lightMat;
            }
            lightQuad.SetActive(false);

            // 4. Toss Coin Presenter Root
            var coinGo = GameObject.Find("TossCoin_Presence");
            if (coinGo == null)
            {
                coinGo = new GameObject("TossCoin_Presence");
            }

            // Position at comfortable observation distance: 0.40m in front of eye level (1.6m high)
            coinGo.transform.position = new Vector3(0f, 1.6f, config.defaultObservationDistance);
            coinGo.transform.rotation = Quaternion.Euler(config.defaultDisplayAngle);

            var meshFilter = coinGo.GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = coinGo.AddComponent<MeshFilter>();

            var meshRenderer = coinGo.GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = coinGo.AddComponent<MeshRenderer>();

            var presenter = coinGo.GetComponent<CoinPresenter>();
            if (presenter == null) presenter = coinGo.AddComponent<CoinPresenter>();

            meshFilter.sharedMesh = coinMesh;
            meshRenderer.sharedMaterial = coinMat;
            presenter.config = config;
            presenter.SetMaterialAndTextures(coinMat, normalTex, aoTex);
            presenter.SetBackdropReferences(darkQuad, lightQuad);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        [MenuItem("Toss/P1 Audit Coin Presence Baseline")]
        public static void AuditCoinPresenceBaseline()
        {
            Debug.Log("=================================================");
            Debug.Log("[P1.1 Audit] AUDITING COIN PRESENCE BASELINE");
            Debug.Log("=================================================");

            bool sceneExists = File.Exists(ScenePath);
            bool meshExists = File.Exists(MeshAssetPath);
            bool matExists = File.Exists(MatAssetPath);
            bool normalExists = File.Exists(NormalTexPath);
            bool aoExists = File.Exists(AoTexPath);

            Debug.Log($"Scene Asset Exists:        {sceneExists} ({ScenePath})");
            Debug.Log($"Mesh Asset Exists:         {meshExists} ({MeshAssetPath})");
            Debug.Log($"Material Asset Exists:     {matExists} ({MatAssetPath})");
            Debug.Log($"Normal Map Asset Exists:   {normalExists} ({NormalTexPath})");
            Debug.Log($"AO Map Asset Exists:       {aoExists} ({AoTexPath})");

            var config = new CoinVisualConfig();
            Debug.Log($"Diameter:                  {config.diameter * 1000f:F1} mm (Target 30mm)");
            Debug.Log($"Thickness:                 {config.thickness * 1000f:F2} mm (Target 2.4mm)");
            Debug.Log($"Edge Ridge Count:          {config.edgeRidgeCount} (Target 48)");
            Debug.Log($"Edge Ridge Depth:          {config.edgeRidgeDepth * 1000f:F2} mm");
            Debug.Log($"Metal Roughness:           {config.metalRoughness:F2} (Satin silver)");
            Debug.Log($"Metallic:                  {config.metallic:F2}");
            Debug.Log($"Metal Tint (RGBA):         {config.metalTint}");
            Debug.Log($"Observation Distance:      {config.defaultObservationDistance:F2} m");
            Debug.Log("=================================================");
        }
    }
}
