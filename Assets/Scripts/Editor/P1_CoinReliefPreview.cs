using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Toss.Editor
{
    public static class P1_CoinReliefPreview
    {
        private const string FbxPath = "Assets/Meshes/Coin/TossCoin_Heads_Relief.fbx";
        private const string MatPath = "Assets/Materials/Coin/M_TossCoin_Heads_PBR.mat";
        private const string OutDir = "Assets/Textures/Coin";

        [MenuItem("Toss/P1 Generate Heads Relief Previews")]
        public static void GeneratePreviews()
        {
            Debug.Log("[P1 Heads Preview] Starting preview generation pipeline...");

            // 1. Ensure FBX is imported with optimal settings
            AssetDatabase.Refresh();
            ConfigureModelImporter();

            // 2. Load Mesh from FBX
            var fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbxPrefab == null)
            {
                Debug.LogError($"[P1 Heads Preview] Failed to load FBX at {FbxPath}");
                return;
            }

            var meshFilter = fbxPrefab.GetComponentInChildren<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogError("[P1 Heads Preview] No MeshFilter / Mesh found in FBX!");
                return;
            }
            Mesh coinMesh = meshFilter.sharedMesh;
            Debug.Log($"[P1 Heads Preview] Loaded mesh: {coinMesh.name}, Vertices: {coinMesh.vertexCount}, Triangles: {coinMesh.triangles.Length / 3}");

            // 3. Create or update PBR Material
            Material coinMat = EnsurePbrMaterial();

            // 4. Setup temporary preview scene root
            var rootGo = new GameObject("Preview_Coin_Stage");
            var coinGo = new GameObject("TossCoin_Instance");
            coinGo.transform.SetParent(rootGo.transform, false);

            var mf = coinGo.AddComponent<MeshFilter>();
            mf.sharedMesh = coinMesh;
            var mr = coinGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = coinMat;

            // Setup Lights
            var lightGo1 = new GameObject("KeyLight");
            lightGo1.transform.SetParent(rootGo.transform, false);
            var keyLight = lightGo1.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1.0f, 0.98f, 0.95f);
            keyLight.intensity = 1.35f;
            keyLight.transform.rotation = Quaternion.Euler(38f, -48f, 0f);

            var lightGo2 = new GameObject("FillLight");
            lightGo2.transform.SetParent(rootGo.transform, false);
            var fillLight = lightGo2.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.85f, 0.90f, 1.0f);
            fillLight.intensity = 0.50f;
            fillLight.transform.rotation = Quaternion.Euler(-25f, 135f, 0f);

            var lightGo3 = new GameObject("RimKickerLight");
            lightGo3.transform.SetParent(rootGo.transform, false);
            var rimLight = lightGo3.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.color = new Color(0.95f, 0.95f, 1.0f);
            rimLight.intensity = 0.65f;
            rimLight.transform.rotation = Quaternion.Euler(15f, 120f, 0f);

            // Setup Camera
            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(rootGo.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.backgroundColor = new Color(0.08f, 0.085f, 0.09f, 1f); // Dark studio backdrop
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 28f;
            cam.nearClipPlane = 0.005f;
            cam.farClipPlane = 20f;

            // Render 4 Views
            int res = 1024;
            var rt = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 8;
            cam.targetTexture = rt;

            Directory.CreateDirectory(OutDir);

            // View 1: 0° 正视图 (Front View - Heads)
            cam.transform.position = new Vector3(0f, 0f, 0.085f);
            cam.transform.LookAt(new Vector3(0f, 0f, 0f));
            RenderAndSave(cam, rt, Path.Combine(OutDir, "View_1_Heads_Front.png"));
            RenderAndSave(cam, rt, Path.Combine(OutDir, "Unity_Play_View_0deg_Front.png"));

            // View 2: 45° 斜视图 (45° Oblique View - Heads)
            cam.transform.position = new Vector3(-0.052f, 0.048f, 0.052f);
            cam.transform.LookAt(new Vector3(0f, 0f, 0.0005f));
            RenderAndSave(cam, rt, Path.Combine(OutDir, "View_2_Heads_Angle45.png"));
            RenderAndSave(cam, rt, Path.Combine(OutDir, "Unity_Play_View_45deg_Oblique.png"));

            // View 3: 侧前方低角度视图 (Low-Angle Side View showing thickness & reeded edge)
            cam.transform.position = new Vector3(-0.042f, -0.016f, 0.022f);
            cam.transform.LookAt(new Vector3(-0.002f, 0f, 0.0008f));
            RenderAndSave(cam, rt, Path.Combine(OutDir, "View_3_Heads_LowAngle.png"));

            // View 4: 近景细节图 (Close-Up Macro on Face & Hair)
            cam.transform.position = new Vector3(-0.011f, 0.006f, 0.038f);
            cam.transform.LookAt(new Vector3(-0.003f, 0.002f, 0.001f));
            RenderAndSave(cam, rt, Path.Combine(OutDir, "View_4_Heads_Closeup.png"));

            // Tails Views (-Z face rotated 180° around Y)
            coinGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // View 5: 0° 背面正视图 (Tails Front View)
            cam.transform.position = new Vector3(0f, 0f, 0.085f);
            cam.transform.LookAt(new Vector3(0f, 0f, 0f));
            RenderAndSave(cam, rt, Path.Combine(OutDir, "View_5_Tails_Front.png"));
            RenderAndSave(cam, rt, Path.Combine(OutDir, "Unity_Play_View_180deg_Tails.png"));

            // View 6: 45° 背面斜视图 (Tails 45° Oblique View)
            cam.transform.position = new Vector3(-0.052f, 0.048f, 0.052f);
            cam.transform.LookAt(new Vector3(0f, 0f, 0.0005f));
            RenderAndSave(cam, rt, Path.Combine(OutDir, "View_6_Tails_Angle45.png"));
            RenderAndSave(cam, rt, Path.Combine(OutDir, "Unity_Play_View_135deg_Tails_Oblique.png"));

            // View 7: 背面近景细节图 (Tails Closeup Macro on Bird)
            cam.transform.position = new Vector3(-0.011f, 0.006f, 0.038f);
            cam.transform.LookAt(new Vector3(-0.003f, 0.002f, 0.001f));
            RenderAndSave(cam, rt, Path.Combine(OutDir, "View_7_Tails_Closeup.png"));

            // Cleanup
            cam.targetTexture = null;
            RenderTexture.active = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(rootGo);

            // Generate Composite Images
            CreateComposite4Views();
            CreateCompositeHeadsAndTails();

            AssetDatabase.Refresh();
            Debug.Log("[P1 Coin Preview] All Heads & Tails views generated successfully in " + OutDir);
        }

        private static void ConfigureModelImporter()
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer != null)
            {
                bool modified = false;
                if (Mathf.Abs(importer.globalScale - 1f) > 0.001f)
                {
                    importer.globalScale = 1.0f;
                    modified = true;
                }
                if (importer.importNormals != ModelImporterNormals.Import)
                {
                    importer.importNormals = ModelImporterNormals.Import;
                    importer.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;
                    modified = true;
                }
                if (importer.importTangents != ModelImporterTangents.CalculateMikk)
                {
                    importer.importTangents = ModelImporterTangents.CalculateMikk;
                    modified = true;
                }
                if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
                {
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    modified = true;
                }
                if (modified)
                {
                    importer.SaveAndReimport();
                    Debug.Log("[P1 Heads Preview] ModelImporter configured and reimported.");
                }
            }
        }

        private static Material EnsurePbrMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "M_TossCoin_Heads_PBR" };
                AssetDatabase.CreateAsset(mat, MatPath);
            }

            // PBR Silver Satin settings
            mat.SetColor("_BaseColor", new Color(0.88f, 0.89f, 0.91f, 1f));
            mat.SetFloat("_Metallic", 0.94f);
            mat.SetFloat("_Smoothness", 0.60f);

            // Clear normal map to ensure 100% real geometry form without conflicting normal fake!
            mat.SetTexture("_BumpMap", null);
            mat.DisableKeyword("_NORMALMAP");

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static void RenderAndSave(Camera cam, RenderTexture rt, string savePath)
        {
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(savePath, bytes);
            string artifactDir = @"C:\Users\steve\.gemini\antigravity\brain\0277133a-dfeb-4f06-a221-280321d0da00";
            if (Directory.Exists(artifactDir))
            {
                File.WriteAllBytes(Path.Combine(artifactDir, Path.GetFileName(savePath)), bytes);
            }
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log($"[P1 Coin Preview] Saved view: {savePath}");
        }

        private static void CreateComposite4Views()
        {
            string p1 = Path.Combine(OutDir, "View_1_Heads_Front.png");
            string p2 = Path.Combine(OutDir, "View_2_Heads_Angle45.png");
            string p3 = Path.Combine(OutDir, "View_3_Heads_LowAngle.png");
            string p4 = Path.Combine(OutDir, "View_4_Heads_Closeup.png");

            if (!File.Exists(p1) || !File.Exists(p2) || !File.Exists(p3) || !File.Exists(p4))
                return;

            byte[] b1 = File.ReadAllBytes(p1);
            byte[] b2 = File.ReadAllBytes(p2);
            byte[] b3 = File.ReadAllBytes(p3);
            byte[] b4 = File.ReadAllBytes(p4);

            var t1 = new Texture2D(2, 2); t1.LoadImage(b1);
            var t2 = new Texture2D(2, 2); t2.LoadImage(b2);
            var t3 = new Texture2D(2, 2); t3.LoadImage(b3);
            var t4 = new Texture2D(2, 2); t4.LoadImage(b4);

            int subW = t1.width;
            int subH = t1.height;
            var comp = new Texture2D(subW * 2, subH * 2, TextureFormat.RGB24, false);

            // Top-Left: Front (0°)
            comp.SetPixels(0, subH, subW, subH, t1.GetPixels());
            // Top-Right: 45° Oblique
            comp.SetPixels(subW, subH, subW, subH, t2.GetPixels());
            // Bottom-Left: Low Angle Side
            comp.SetPixels(0, 0, subW, subH, t3.GetPixels());
            // Bottom-Right: Close-Up
            comp.SetPixels(subW, 0, subW, subH, t4.GetPixels());

            comp.Apply();
            string compPath = Path.Combine(OutDir, "View_Heads_4Views_Composite.png");
            File.WriteAllBytes(compPath, comp.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(t1);
            UnityEngine.Object.DestroyImmediate(t2);
            UnityEngine.Object.DestroyImmediate(t3);
            UnityEngine.Object.DestroyImmediate(t4);
            UnityEngine.Object.DestroyImmediate(comp);

            Debug.Log($"[P1 Heads Preview] Saved composite view to {compPath}");
        }

        private static void CreateCompositeHeadsAndTails()
        {
            string p1 = Path.Combine(OutDir, "View_1_Heads_Front.png");
            string p2 = Path.Combine(OutDir, "View_2_Heads_Angle45.png");
            string p5 = Path.Combine(OutDir, "View_5_Tails_Front.png");
            string p6 = Path.Combine(OutDir, "View_6_Tails_Angle45.png");

            if (!File.Exists(p1) || !File.Exists(p2) || !File.Exists(p5) || !File.Exists(p6))
                return;

            byte[] b1 = File.ReadAllBytes(p1);
            byte[] b2 = File.ReadAllBytes(p2);
            byte[] b5 = File.ReadAllBytes(p5);
            byte[] b6 = File.ReadAllBytes(p6);

            var t1 = new Texture2D(2, 2); t1.LoadImage(b1);
            var t2 = new Texture2D(2, 2); t2.LoadImage(b2);
            var t5 = new Texture2D(2, 2); t5.LoadImage(b5);
            var t6 = new Texture2D(2, 2); t6.LoadImage(b6);

            int subW = t1.width;
            int subH = t1.height;
            var comp = new Texture2D(subW * 2, subH * 2, TextureFormat.RGB24, false);

            // Top-Left: Heads Front (0°)
            comp.SetPixels(0, subH, subW, subH, t1.GetPixels());
            // Top-Right: Heads 45° Oblique
            comp.SetPixels(subW, subH, subW, subH, t2.GetPixels());
            // Bottom-Left: Tails Front (180°)
            comp.SetPixels(0, 0, subW, subH, t5.GetPixels());
            // Bottom-Right: Tails 45° Oblique
            comp.SetPixels(subW, 0, subW, subH, t6.GetPixels());

            comp.Apply();
            byte[] bytes = comp.EncodeToPNG();

            string compPath = Path.Combine(OutDir, "Unity_Heads_Tails_Comparison.png");
            File.WriteAllBytes(compPath, bytes);

            string artifactDir = @"C:\Users\steve\.gemini\antigravity\brain\0277133a-dfeb-4f06-a221-280321d0da00";
            if (Directory.Exists(artifactDir))
            {
                File.WriteAllBytes(Path.Combine(artifactDir, "Unity_Heads_Tails_Comparison.png"), bytes);
            }

            UnityEngine.Object.DestroyImmediate(t1);
            UnityEngine.Object.DestroyImmediate(t2);
            UnityEngine.Object.DestroyImmediate(t5);
            UnityEngine.Object.DestroyImmediate(t6);
            UnityEngine.Object.DestroyImmediate(comp);

            Debug.Log($"[P1 Coin Preview] Saved Heads & Tails comparison sheet to {compPath}");
        }
    }
}
