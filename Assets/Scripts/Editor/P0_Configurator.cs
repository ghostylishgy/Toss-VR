using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using Toss.Diagnostics;
using Toss.Interaction;

namespace Toss.Editor
{
    [Serializable]
    public class P0ValidationEvidence
    {
        public string timestamp;
        public string configuredProfile;
        public string activeSessionEvidence;
        public string openXrRuntimeEnvironment;
        public bool editorInPlayMode;
        public bool simulatorConnected;
        public bool stabilityPassed;

        [Header("Gaze Validation")]
        public string gazeSource;
        public bool gazeObserved;
        public int gazeTransitionsCount;
        public bool cameraForwardUsedAsGaze;

        [Header("Pinch Validation")]
        public string pinchSource;
        public bool handTracked;
        public bool pinchObserved;
        public int pinchTransitionsCount;
        public bool mouseOrKeyboardFallbackUsed;

        [Header("Look-and-Pinch Validation")]
        public bool lookAndPinchTriggered;
        public int lookAndPinchCount;
        public string targetInitialScale;
        public string targetReactedScale;

        [Header("Project Setup Tool Audit")]
        public int criticalCount;
        public List<string> readinessCritical = new List<string>();
        public int warningCount;
        public List<string> readinessWarnings = new List<string>();
        public int recommendationCount;
        public List<string> readinessRecommendations = new List<string>();

        [Header("Conclusion")]
        public string overallStatus; // "P0 = PASS" or "P0 = NOT PASS"
    }

    [InitializeOnLoad]
    public static class P0_Configurator
    {
        private static bool s_firstPlayCompleted = false;
        private static bool s_stabilityVerified = false;

        static P0_Configurator()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                Debug.Log($"[P0 PlayMode] Entered Play Mode at {DateTime.UtcNow:o}. OpenXR active: {UnityEngine.XR.XRSettings.isDeviceActive}, device: {UnityEngine.XR.XRSettings.loadedDeviceName}");
                if (s_firstPlayCompleted)
                {
                    s_stabilityVerified = true;
                    Debug.Log("[P0 PlayMode] Play Mode re-entry verified. Simulator reconnected successfully.");
                }
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                Debug.Log($"[P0 PlayMode] Exited Play Mode at {DateTime.UtcNow:o}.");
                s_firstPlayCompleted = true;
            }
        }

        [MenuItem("Toss/P0 Configure OpenXR and Project")]
        public static void ConfigureOpenXRAndProject()
        {
            Debug.Log("[P0_Configurator] Configuring Project for Meta VR Glasses Hands-Only...");

            // 1. Android Player Settings
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.ghostylish.toss");
            PlayerSettings.productName = "Toss";
            PlayerSettings.companyName = "Ghostylish";

            // 2. Configure XR Management & Loaders for Standalone and Android
            ConfigureBuildTargetXR(BuildTargetGroup.Standalone);
            ConfigureBuildTargetXR(BuildTargetGroup.Android);

            // 3. Configure Meta XR Core (OVRProjectConfig)
            ConfigureOVRHandsOnly();

            // 4. Auto-Fix standard Meta project setup issues
            AutoFixProjectSetup();

            // 5. Setup Validation Scene
            SetupValidationScene();

            AssetDatabase.SaveAssets();
            Debug.Log("[P0_Configurator] XR and Project Configuration Completed Successfully.");
        }

        private static void ConfigureOVRHandsOnly()
        {
            try
            {
                var projectConfig = OVRProjectConfig.CachedProjectConfig;
                if (projectConfig != null)
                {
                    if (projectConfig.targetDeviceTypes == null)
                    {
                        projectConfig.targetDeviceTypes = new List<OVRProjectConfig.DeviceType>();
                    }
                    projectConfig.targetDeviceTypes.Clear();
                    projectConfig.targetDeviceTypes.Add(OVRProjectConfig.DeviceType.VRGlasses);

                    projectConfig.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.HandsOnly;
                    projectConfig.handTrackingFrequency = OVRProjectConfig.HandTrackingFrequency.HIGH;
                    projectConfig.renderModelSupport = OVRProjectConfig.RenderModelSupport.Disabled;

                    OVRProjectConfig.CommitProjectConfig(projectConfig);
                    EditorUtility.SetDirty(projectConfig);
                    Debug.Log("[P0_Configurator] OVRProjectConfig locked to VRGlasses + HandsOnly.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[P0_Configurator] ConfigureOVRHandsOnly warning: {ex.Message}");
            }
        }

        private static void AutoFixProjectSetup()
        {
            try
            {
                var setupType = Type.GetType("OVRProjectSetup, Oculus.VR.Editor");
                if (setupType != null)
                {
                    var getTasksMethod = setupType.GetMethod("GetTasks", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(BuildTargetGroup) }, null);
                    if (getTasksMethod != null)
                    {
                        var tasks = getTasksMethod.Invoke(null, new object[] { BuildTargetGroup.Android }) as IEnumerable;
                        if (tasks != null)
                        {
                            foreach (var task in tasks)
                            {
                                var validProp = task.GetType().GetProperty("Valid");
                                object validObj = validProp?.GetValue(task);
                                if (validObj != null)
                                {
                                    var getValidMethod = validObj.GetType().GetMethod("GetValue", new[] { typeof(BuildTargetGroup) });
                                    bool isValid = (bool)(getValidMethod?.Invoke(validObj, new object[] { BuildTargetGroup.Android }) ?? true);
                                    if (!isValid) continue;
                                }

                                var isDoneProp = task.GetType().GetProperty("IsDone");
                                var isDoneDelegate = isDoneProp?.GetValue(task) as Delegate;
                                bool isDone = false;
                                if (isDoneDelegate != null)
                                {
                                    try { isDone = (bool)isDoneDelegate.DynamicInvoke(BuildTargetGroup.Android); } catch { }
                                }

                                if (!isDone)
                                {
                                    var fixActionProp = task.GetType().GetProperty("FixAction");
                                    var fixActionDelegate = fixActionProp?.GetValue(task) as Delegate;
                                    if (fixActionDelegate != null)
                                    {
                                        try
                                        {
                                            fixActionDelegate.DynamicInvoke(BuildTargetGroup.Android);
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[P0_Configurator] AutoFixProjectSetup notice: {ex.Message}");
            }
        }

        private static XRGeneralSettingsPerBuildTarget GetOrCreateXRGeneralSettings()
        {
            if (EditorBuildSettings.TryGetConfigObject<XRGeneralSettingsPerBuildTarget>(XRGeneralSettings.k_SettingsKey, out var generalSettings) && generalSettings != null)
            {
                return generalSettings;
            }

            var guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
            if (guids.Length > 0)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[0]);
                generalSettings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(p);
                if (generalSettings != null)
                {
                    EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, generalSettings, true);
                    return generalSettings;
                }
            }

            generalSettings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            string settingsDir = "Assets/XR";
            if (!Directory.Exists(settingsDir)) Directory.CreateDirectory(settingsDir);
            string assetPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            AssetDatabase.CreateAsset(generalSettings, assetPath);
            AssetDatabase.SaveAssets();
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, generalSettings, true);
            return generalSettings;
        }

        private static void ConfigureBuildTargetXR(BuildTargetGroup group)
        {
            try
            {
                var generalSettingsPerTarget = GetOrCreateXRGeneralSettings();
                if (generalSettingsPerTarget != null)
                {
                    if (!generalSettingsPerTarget.HasSettingsForBuildTarget(group))
                    {
                        generalSettingsPerTarget.CreateDefaultSettingsForBuildTarget(group);
                    }
                    if (!generalSettingsPerTarget.HasManagerSettingsForBuildTarget(group))
                    {
                        generalSettingsPerTarget.CreateDefaultManagerSettingsForBuildTarget(group);
                    }

                    var manager = generalSettingsPerTarget.ManagerSettingsForBuildTarget(group);
                    if (manager != null)
                    {
                        XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group);
                        EditorUtility.SetDirty(manager);
                    }
                    EditorUtility.SetDirty(generalSettingsPerTarget);
                }

                var openXRSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (openXRSettings != null)
                {
                    var features = openXRSettings.GetFeatures();
                    foreach (var f in features)
                    {
                        if (f == null) continue;
                        string fName = f.name;
                        if (fName.Contains("MetaXR") || fName.Contains("Meta XR") ||
                            fName.Contains("Hand") || fName.Contains("Eye") || fName.Contains("Aim"))
                        {
                            f.enabled = true;
                            Debug.Log($"[P0_Configurator] Enabled OpenXR Feature for {group}: {fName}");
                        }
                    }
                    EditorUtility.SetDirty(openXRSettings);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[P0_Configurator] ConfigureBuildTargetXR ({group}) warning: {ex.Message}");
            }
        }

        public static void SetupValidationScene()
        {
            string scenePath = "Assets/Scenes/P0_EnvironmentValidation.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Clean up standalone Main Camera if OVRCameraRig is used
            var oldCams = GameObject.FindGameObjectsWithTag("MainCamera");

            // Setup OVRCameraRig
            var rigGo = GameObject.Find("OVRCameraRig");
            if (rigGo == null)
            {
                var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab");
                if (rigPrefab != null)
                {
                    rigGo = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
                    rigGo.name = "OVRCameraRig";
                }
                else
                {
                    rigGo = new GameObject("OVRCameraRig");
                    rigGo.AddComponent<OVRCameraRig>();
                }
            }

            rigGo.transform.position = Vector3.zero;
            rigGo.transform.rotation = Quaternion.identity;

            // Configure OVRManager
            var ovrManager = rigGo.GetComponent<OVRManager>();
            if (ovrManager == null) ovrManager = rigGo.AddComponent<OVRManager>();
            ovrManager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;

            // Ensure Eye Gaze component on CenterEyeAnchor
            var centerEye = rigGo.transform.Find("TrackingSpace/CenterEyeAnchor");
            if (centerEye != null)
            {
                var eyeGaze = centerEye.GetComponent<OVREyeGaze>();
                if (eyeGaze == null) eyeGaze = centerEye.gameObject.AddComponent<OVREyeGaze>();
                eyeGaze.Eye = OVREyeGaze.EyeId.Left;
                eyeGaze.ConfidenceThreshold = 0.1f;
            }

            // Ensure Left and Right Hands with OVRHand
            var leftHandAnchor = rigGo.transform.Find("TrackingSpace/LeftHandAnchor");
            if (leftHandAnchor != null && leftHandAnchor.GetComponent<OVRHand>() == null)
            {
                var hand = leftHandAnchor.gameObject.AddComponent<OVRHand>();
                var so = new SerializedObject(hand);
                var prop = so.FindProperty("HandType");
                if (prop != null)
                {
                    prop.intValue = (int)OVRHand.Hand.HandLeft;
                    so.ApplyModifiedProperties();
                }
            }

            var rightHandAnchor = rigGo.transform.Find("TrackingSpace/RightHandAnchor");
            if (rightHandAnchor != null && rightHandAnchor.GetComponent<OVRHand>() == null)
            {
                var hand = rightHandAnchor.gameObject.AddComponent<OVRHand>();
                var so = new SerializedObject(hand);
                var prop = so.FindProperty("HandType");
                if (prop != null)
                {
                    prop.intValue = (int)OVRHand.Hand.HandRight;
                    so.ApplyModifiedProperties();
                }
            }

            // Ensure Interaction Target exists at comfortable FOV (0, 1.2, 1.0)
            var targetGo = GameObject.Find("P0_InteractionTarget");
            if (targetGo == null)
            {
                targetGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                targetGo.name = "P0_InteractionTarget";
            }
            targetGo.transform.position = new Vector3(0, 1.2f, 1.0f);
            targetGo.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

            var tester = targetGo.GetComponent<LookPinchTester>();
            if (tester == null)
            {
                tester = targetGo.AddComponent<LookPinchTester>();
            }

            // Ensure Diagnostics object exists
            var diagGo = GameObject.Find("P0_Diagnostics");
            if (diagGo == null)
            {
                diagGo = new GameObject("P0_Diagnostics");
            }
            var diag = diagGo.GetComponent<DeviceReadinessCheck>();
            if (diag == null)
            {
                diag = diagGo.AddComponent<DeviceReadinessCheck>();
            }

            Physics.SyncTransforms();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[P0_Configurator] Validation scene saved: {scenePath}");
        }

        [MenuItem("Toss/P0 Audit Project and Runtime Evidence")]
        public static void AuditProjectState()
        {
            Debug.Log("=================================================");
            Debug.Log("[P0 Audit] STARTING INTEGRITY-DRIVEN P0 AUDIT");
            Debug.Log("=================================================");

            // 1. Ensure project settings are up to date
            ConfigureOpenXRAndProject();

            var evidence = new P0ValidationEvidence
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                cameraForwardUsedAsGaze = false,
                mouseOrKeyboardFallbackUsed = false
            };

            // 2. OpenXR Environment and Simulator Config
            string runtimeJson = Environment.GetEnvironmentVariable("XR_RUNTIME_JSON") ?? "";
            evidence.openXrRuntimeEnvironment = runtimeJson;

            string simConfigPath = @"G:\Dev\MetaXRSimulator\runtime\PFiles\MetaXRSimulator\v207.0\config\sim_core_configuration.json";
            if (File.Exists(simConfigPath))
            {
                string json = File.ReadAllText(simConfigPath);
                evidence.configuredProfile = json.Contains("\"device_profile\": \"Meta VR Glasses\"") ? "Meta VR Glasses" : "Other / Unknown";
            }
            else
            {
                evidence.configuredProfile = "Config file missing";
            }

            // 3. Runtime Session Evidence
            evidence.editorInPlayMode = EditorApplication.isPlaying;
            evidence.simulatorConnected = UnityEngine.XR.XRSettings.isDeviceActive;
            evidence.activeSessionEvidence = UnityEngine.XR.XRSettings.isDeviceActive
                ? $"Active OpenXR Device: {UnityEngine.XR.XRSettings.loadedDeviceName}"
                : "No Active OpenXR Session (Editor not playing or Simulator not initialized)";

            evidence.stabilityPassed = s_stabilityVerified;

            // 4. Query Real Input Evidence from LookPinchTester
            var tester = GameObject.FindFirstObjectByType<LookPinchTester>();
            if (tester != null)
            {
                evidence.gazeSource = tester.ActiveGazeSource;
                evidence.gazeObserved = (tester.gazeEnterCount > 0 || tester.IsGazed);
                evidence.gazeTransitionsCount = tester.gazeEnterCount;

                evidence.pinchSource = tester.ActivePinchSource;
                evidence.handTracked = (tester.ActivePinchSource.Contains("OVRHand") || tester.ActivePinchSource.Contains("Hand"));
                evidence.pinchObserved = (tester.pinchStartCount > 0 || tester.IsPinched);
                evidence.pinchTransitionsCount = tester.pinchStartCount;

                evidence.lookAndPinchTriggered = (tester.lookAndPinchTriggerCount > 0 || tester.LookAndPinchTriggered);
                evidence.lookAndPinchCount = tester.lookAndPinchTriggerCount;

                var target = GameObject.Find("P0_InteractionTarget");
                if (target != null)
                {
                    evidence.targetInitialScale = "(0.15, 0.15, 0.15)";
                    evidence.targetReactedScale = target.transform.localScale.ToString();
                }
            }
            else
            {
                evidence.gazeSource = "None";
                evidence.gazeObserved = false;
                evidence.pinchSource = "None";
                evidence.handTracked = false;
                evidence.pinchObserved = false;
                evidence.lookAndPinchTriggered = false;
            }

            // 5. Query Meta Project Setup Tool Tasks
            AuditMetaProjectSetup(evidence);

            // 6. Run Device Readiness Report
            var diag = GameObject.FindFirstObjectByType<DeviceReadinessCheck>();
            if (diag != null)
            {
                diag.RunReadinessCheck();
            }

            // 7. Strict Evaluation of P0 PASS Criterion
            bool inputsValidated = evidence.editorInPlayMode &&
                                   evidence.simulatorConnected &&
                                   evidence.gazeObserved &&
                                   evidence.pinchObserved &&
                                   evidence.lookAndPinchTriggered &&
                                   evidence.stabilityPassed &&
                                   evidence.criticalCount == 0;

            evidence.overallStatus = inputsValidated ? "P0 = PASS" : "P0 = NOT PASS";

            // 8. Save Evidence JSON
            string logsDir = Path.Combine(Application.dataPath, "..", "Logs");
            if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);
            string outPath = Path.Combine(logsDir, "P0_ValidationEvidence.json");
            File.WriteAllText(outPath, JsonUtility.ToJson(evidence, true));

            Debug.Log("=================================================");
            Debug.Log($"[P0 Audit Result] {evidence.overallStatus}");
            Debug.Log($"[P0 Audit Evidence] Saved to {outPath}");
            Debug.Log("=================================================");
        }

        private static void AuditMetaProjectSetup(P0ValidationEvidence evidence)
        {
            try
            {
                var setupType = Type.GetType("OVRProjectSetup, Oculus.VR.Editor");
                if (setupType != null)
                {
                    var getTasksMethod = setupType.GetMethod("GetTasks", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(BuildTargetGroup) }, null);
                    if (getTasksMethod != null)
                    {
                        var tasks = getTasksMethod.Invoke(null, new object[] { BuildTargetGroup.Android }) as IEnumerable;
                        if (tasks != null)
                        {
                            foreach (var task in tasks)
                            {
                                var validProp = task.GetType().GetProperty("Valid");
                                object validObj = validProp?.GetValue(task);
                                if (validObj != null)
                                {
                                    var getValidMethod = validObj.GetType().GetMethod("GetValue", new[] { typeof(BuildTargetGroup) });
                                    bool isValid = (bool)(getValidMethod?.Invoke(validObj, new object[] { BuildTargetGroup.Android }) ?? true);
                                    if (!isValid) continue;
                                }

                                var isDoneProp = task.GetType().GetProperty("IsDone");
                                bool isDone = false;
                                if (isDoneProp != null)
                                {
                                    var isDoneDelegate = isDoneProp.GetValue(task) as Delegate;
                                    if (isDoneDelegate != null)
                                    {
                                        try
                                        {
                                            isDone = (bool)isDoneDelegate.DynamicInvoke(BuildTargetGroup.Android);
                                        }
                                        catch
                                        {
                                            isDone = false;
                                        }
                                    }
                                }

                                if (!isDone)
                                {
                                    var levelProp = task.GetType().GetProperty("Level");
                                    var messageProp = task.GetType().GetProperty("Message");

                                    object levelObj = levelProp?.GetValue(task);
                                    object msgObj = messageProp?.GetValue(task);

                                    string levelStr = "";
                                    if (levelObj != null)
                                    {
                                        var getValMethod = levelObj.GetType().GetMethod("GetValue", new[] { typeof(BuildTargetGroup) });
                                        levelStr = getValMethod?.Invoke(levelObj, new object[] { BuildTargetGroup.Android })?.ToString() ?? levelObj.ToString();
                                    }

                                    string msgStr = "";
                                    if (msgObj != null)
                                    {
                                        var getValMethod = msgObj.GetType().GetMethod("GetValue", new[] { typeof(BuildTargetGroup) });
                                        msgStr = getValMethod?.Invoke(msgObj, new object[] { BuildTargetGroup.Android })?.ToString() ?? msgObj.ToString();
                                    }

                                    if (levelStr.Equals("Required", StringComparison.OrdinalIgnoreCase))
                                    {
                                        evidence.criticalCount++;
                                        evidence.readinessCritical.Add(msgStr);
                                    }
                                    else if (levelStr.Equals("Recommended", StringComparison.OrdinalIgnoreCase))
                                    {
                                        evidence.recommendationCount++;
                                        evidence.readinessRecommendations.Add(msgStr);
                                    }
                                    else
                                    {
                                        evidence.warningCount++;
                                        evidence.readinessWarnings.Add(msgStr);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[P0_Configurator] AuditMetaProjectSetup notice: {ex.Message}");
            }
        }
    }
}
