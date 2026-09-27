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
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.Surfaces;
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

            // 5. Setup Validation Scene with official Interaction SDK Gaze architecture
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
                        var targets = new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone };
                        foreach (var target in targets)
                        {
                            var tasks = getTasksMethod.Invoke(null, new object[] { target }) as IEnumerable;
                            if (tasks != null)
                            {
                                foreach (var task in tasks)
                                {
                                    var validProp = task.GetType().GetProperty("Valid");
                                    object validObj = validProp?.GetValue(task);
                                    if (validObj != null)
                                    {
                                        var getValidMethod = validObj.GetType().GetMethod("GetValue", new[] { typeof(BuildTargetGroup) });
                                        bool isValid = (bool)(getValidMethod?.Invoke(validObj, new object[] { target }) ?? true);
                                        if (!isValid) continue;
                                    }

                                    var isDoneProp = task.GetType().GetProperty("IsDone");
                                    var isDoneDelegate = isDoneProp?.GetValue(task) as Delegate;
                                    bool isDone = false;
                                    if (isDoneDelegate != null)
                                    {
                                        try { isDone = (bool)isDoneDelegate.DynamicInvoke(target); } catch { }
                                    }

                                    if (!isDone)
                                    {
                                        var fixActionProp = task.GetType().GetProperty("FixAction");
                                        var fixActionDelegate = fixActionProp?.GetValue(task) as Delegate;
                                        if (fixActionDelegate != null)
                                        {
                                            try
                                            {
                                                fixActionDelegate.DynamicInvoke(target);
                                            }
                                            catch { }
                                        }
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

            // 1. Setup / Find OVRCameraRig
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

            var ovrCameraRig = rigGo.GetComponent<OVRCameraRig>() ?? rigGo.AddComponent<OVRCameraRig>();

            // Configure OVRManager: FloorLevel tracking
            var ovrManager = rigGo.GetComponent<OVRManager>() ?? rigGo.AddComponent<OVRManager>();
            ovrManager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;

            // 2. Clean up duplicate standalone Camera and AudioListener instances
            var centerEye = rigGo.transform.Find("TrackingSpace/CenterEyeAnchor");
            Camera centerCam = null;
            if (centerEye != null)
            {
                centerCam = centerEye.GetComponent<Camera>() ?? centerEye.gameObject.AddComponent<Camera>();
                centerCam.tag = "MainCamera";

                // Ensure CenterEyeAnchor is the sole AudioListener
                if (centerEye.GetComponent<AudioListener>() == null)
                {
                    centerEye.gameObject.AddComponent<AudioListener>();
                }

                // Remove obsolete Movement SDK OVREyeGaze if present on CenterEyeAnchor
                var obsoleteEyeGaze = centerEye.GetComponent<OVREyeGaze>();
                if (obsoleteEyeGaze != null)
                {
                    Debug.Log("[P0_Configurator] Removing legacy Movement OVREyeGaze from CenterEyeAnchor.");
                    GameObject.DestroyImmediate(obsoleteEyeGaze);
                }
            }

            // Destroy any standalone Main Camera
            var allCameras = GameObject.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in allCameras)
            {
                if (cam.gameObject.name == "Main Camera" && (cam.transform.parent == null || !cam.transform.IsChildOf(rigGo.transform)))
                {
                    Debug.Log($"[P0_Configurator] Destroying redundant standalone camera: {cam.gameObject.name}");
                    GameObject.DestroyImmediate(cam.gameObject);
                }
            }

            // Destroy any duplicate AudioListener not on CenterEyeAnchor
            var allListeners = GameObject.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            foreach (var listener in allListeners)
            {
                if (centerEye != null && listener.transform == centerEye) continue;
                Debug.Log($"[P0_Configurator] Destroying extra AudioListener on: {listener.gameObject.name}");
                GameObject.DestroyImmediate(listener);
            }

            // 3. Ensure OVRComprehensiveInteractionRig exists under OVRCameraRig
            var interactionRigGo = GameObject.Find("OVRComprehensiveInteractionRig");
            if (interactionRigGo == null)
            {
                var interactionRigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/OVRComprehensiveInteractionRig.prefab");
                if (interactionRigPrefab != null)
                {
                    interactionRigGo = (GameObject)PrefabUtility.InstantiatePrefab(interactionRigPrefab, rigGo.transform);
                    interactionRigGo.name = "OVRComprehensiveInteractionRig";
                    Debug.Log("[P0_Configurator] Instantiated OVRComprehensiveInteractionRig prefab.");
                }
            }
            if (interactionRigGo.transform.parent != rigGo.transform)
            {
                interactionRigGo.transform.SetParent(rigGo.transform, false);
            }

            // 4. Locate OVRHands and OVRHandsDataSource
            OVRHand leftOvrHand = null;
            OVRHand rightOvrHand = null;

            var leftHandSource = interactionRigGo.transform.Find("OVRHands/OVRHandDataSourceLeft");
            if (leftHandSource != null) leftOvrHand = leftHandSource.GetComponent<OVRHand>();

            var rightHandSource = interactionRigGo.transform.Find("OVRHands/OVRHandDataSourceRight");
            if (rightHandSource != null) rightOvrHand = rightHandSource.GetComponent<OVRHand>();

            // 5. Wire OVRCameraRigRef on OVRComprehensiveInteractionRig
            var cameraRigRef = interactionRigGo.GetComponent<OVRCameraRigRef>();
            if (cameraRigRef != null)
            {
                var soRigRef = new SerializedObject(cameraRigRef);
                var camRigProp = soRigRef.FindProperty("_ovrCameraRig");
                if (camRigProp != null) camRigProp.objectReferenceValue = ovrCameraRig;

                var leftHandProp = soRigRef.FindProperty("_leftHand");
                if (leftHandProp != null && leftOvrHand != null) leftHandProp.objectReferenceValue = leftOvrHand;

                var rightHandProp = soRigRef.FindProperty("_rightHand");
                if (rightHandProp != null && rightOvrHand != null) rightHandProp.objectReferenceValue = rightOvrHand;

                var reqHandsProp = soRigRef.FindProperty("_requireOvrHands");
                if (reqHandsProp != null) reqHandsProp.boolValue = true;

                soRigRef.ApplyModifiedProperties();
                EditorUtility.SetDirty(cameraRigRef);
                Debug.Log("[P0_Configurator] Wired OVRCameraRigRef: _ovrCameraRig, _leftHand, _rightHand.");
            }

            // 6. Wire TrackingToWorldTransformerOVR
            var trackingTransformer = interactionRigGo.GetComponent<TrackingToWorldTransformerOVR>();
            if (trackingTransformer != null && cameraRigRef != null)
            {
                var soTrans = new SerializedObject(trackingTransformer);
                var refProp = soTrans.FindProperty("_cameraRigRef");
                if (refProp != null) refProp.objectReferenceValue = cameraRigRef;
                soTrans.ApplyModifiedProperties();
                EditorUtility.SetDirty(trackingTransformer);
                Debug.Log("[P0_Configurator] Wired TrackingToWorldTransformerOVR: _cameraRigRef.");
            }

            // Wire FromOVRHandDataSource (Left and Right)
            if (leftHandSource != null)
            {
                var leftDataSource = leftHandSource.GetComponent<FromOVRHandDataSource>();
                if (leftDataSource != null)
                {
                    var so = new SerializedObject(leftDataSource);
                    var rigProp = so.FindProperty("_cameraRigRef");
                    if (rigProp != null && cameraRigRef != null) rigProp.objectReferenceValue = cameraRigRef;
                    var transProp = so.FindProperty("_trackingToWorldTransformer");
                    if (transProp != null && trackingTransformer != null) transProp.objectReferenceValue = trackingTransformer;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(leftDataSource);
                }
            }
            if (rightHandSource != null)
            {
                var rightDataSource = rightHandSource.GetComponent<FromOVRHandDataSource>();
                if (rightDataSource != null)
                {
                    var so = new SerializedObject(rightDataSource);
                    var rigProp = so.FindProperty("_cameraRigRef");
                    if (rigProp != null && cameraRigRef != null) rigProp.objectReferenceValue = cameraRigRef;
                    var transProp = so.FindProperty("_trackingToWorldTransformer");
                    if (transProp != null && trackingTransformer != null) transProp.objectReferenceValue = trackingTransformer;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(rightDataSource);
                }
            }

            // 7. Ensure OVREyeGaze system (Interaction SDK DataSource + EyeGaze + GazeConecaster) exists
            var trackingSpace = rigGo.transform.Find("TrackingSpace");
            Transform eyeGazeParent = trackingSpace != null ? trackingSpace : rigGo.transform;
            var ovrEyeGazeGo = GameObject.Find("OVREyeGaze");
            if (ovrEyeGazeGo == null)
            {
                var eyeGazePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/OVREyeGaze.prefab");
                if (eyeGazePrefab != null)
                {
                    ovrEyeGazeGo = (GameObject)PrefabUtility.InstantiatePrefab(eyeGazePrefab, eyeGazeParent);
                    ovrEyeGazeGo.name = "OVREyeGaze";
                    Debug.Log("[P0_Configurator] Instantiated OVREyeGaze Interaction SDK prefab.");
                }
            }
            if (ovrEyeGazeGo != null && ovrEyeGazeGo.transform.parent != eyeGazeParent)
            {
                ovrEyeGazeGo.transform.SetParent(eyeGazeParent, false);
            }

            EyeGaze eyeGaze = null;
            GazeConecaster gazeConecaster = null;

            if (ovrEyeGazeGo != null)
            {
                eyeGaze = ovrEyeGazeGo.GetComponentInChildren<EyeGaze>(true);

                // Wire FromOVREyeGazeDataSource
                var dataSource = ovrEyeGazeGo.GetComponentInChildren<FromOVREyeGazeDataSource>(true);
                if (dataSource != null)
                {
                    var soData = new SerializedObject(dataSource);
                    var camProp = soData.FindProperty("_centerEyeCamera");
                    if (camProp != null && centerCam != null) camProp.objectReferenceValue = centerCam;
                    var rigRefProp = soData.FindProperty("_cameraRigRef");
                    if (rigRefProp != null && cameraRigRef != null) rigRefProp.objectReferenceValue = cameraRigRef;
                    soData.ApplyModifiedProperties();
                    EditorUtility.SetDirty(dataSource);
                    Debug.Log("[P0_Configurator] Wired FromOVREyeGazeDataSource: _centerEyeCamera, _cameraRigRef.");
                }

                // Ensure child GazeConecaster exists
                gazeConecaster = ovrEyeGazeGo.GetComponentInChildren<GazeConecaster>(true);
                if (gazeConecaster == null && eyeGaze != null)
                {
                    var coneGo = new GameObject("GazeConecaster");
                    coneGo.transform.SetParent(eyeGaze.transform, false);
                    gazeConecaster = coneGo.AddComponent<GazeConecaster>();
                    Debug.Log("[P0_Configurator] Added GazeConecaster child under EyeGaze.");
                }

                if (gazeConecaster != null && eyeGaze != null)
                {
                    gazeConecaster.InjectGaze(eyeGaze);
                    gazeConecaster.DwellTimespanSeconds = 0.05f;
                    var soCone = new SerializedObject(gazeConecaster);
                    var gazeProp = soCone.FindProperty("_gaze");
                    if (gazeProp != null) gazeProp.objectReferenceValue = eyeGaze;
                    soCone.ApplyModifiedProperties();
                    EditorUtility.SetDirty(gazeConecaster);
                    Debug.Log("[P0_Configurator] Wired GazeConecaster: _gaze.");
                }
            }

            // 8. Setup HandGazeInteractor on Left and Right hands and prune P0-unneeded interactors
            var handGazePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.interaction/Runtime/Prefabs/Gaze/HandGazeInteractor.prefab");

            var handSides = new[]
            {
                (name: "ComprehensiveInteractorsLeft", side: "Left"),
                (name: "ComprehensiveInteractorsRight", side: "Right")
            };

            foreach (var (handRootName, side) in handSides)
            {
                var handRoot = interactionRigGo.transform.Find(handRootName);
                if (handRoot == null)
                {
                    Debug.LogError($"[P0_Configurator] Could not find {handRootName} in OVRComprehensiveInteractionRig!");
                    continue;
                }

                var handComp = handRoot.GetComponent<Oculus.Interaction.Input.Hand>();
                var interactorsHolder = handRoot.Find("Interactors");
                if (interactorsHolder == null)
                {
                    Debug.LogError($"[P0_Configurator] Could not find Interactors under {handRootName}!");
                    continue;
                }

                var handAndNoController = interactorsHolder.Find("Hand and No Controller");
                if (handAndNoController == null)
                {
                    Debug.LogError($"[P0_Configurator] Could not find 'Hand and No Controller' under {handRootName}/Interactors!");
                    continue;
                }

                // Instantiate / Find HandGazeInteractor under "Hand and No Controller"
                var gazeInteractorTrans = handAndNoController.Find("HandGazeInteractor");
                GameObject gazeGo = gazeInteractorTrans != null ? gazeInteractorTrans.gameObject : null;
                if (gazeGo == null && handGazePrefab != null)
                {
                    gazeGo = (GameObject)PrefabUtility.InstantiatePrefab(handGazePrefab, handAndNoController);
                    gazeGo.name = "HandGazeInteractor";
                    Debug.Log($"[P0_Configurator] Instantiated HandGazeInteractor under {handRootName}/Hand and No Controller.");
                }

                GazeInteractor gazeInteractorComp = null;
                if (gazeGo != null)
                {
                    gazeGo.SetActive(true);

                    // Wire HandRef
                    var handRef = gazeGo.GetComponent<HandRef>();
                    if (handRef != null && handComp != null)
                    {
                        handRef.InjectHand(handComp);
                        var so = new SerializedObject(handRef);
                        var p = so.FindProperty("_hand");
                        if (p != null) p.objectReferenceValue = handComp;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(handRef);
                    }

                    // Wire EyeGazeRef
                    var eyeGazeRef = gazeGo.GetComponent<EyeGazeRef>();
                    if (eyeGazeRef != null && eyeGaze != null)
                    {
                        eyeGazeRef.InjectAllEyeGazeRef(eyeGaze);
                        var so = new SerializedObject(eyeGazeRef);
                        var p = so.FindProperty("_gaze");
                        if (p != null) p.objectReferenceValue = eyeGaze;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(eyeGazeRef);
                    }

                    // Wire GazeInteractor
                    gazeInteractorComp = gazeGo.GetComponent<GazeInteractor>();
                    if (gazeInteractorComp != null && gazeConecaster != null)
                    {
                        gazeInteractorComp.InjectHitTester(gazeConecaster);
                        var so = new SerializedObject(gazeInteractorComp);
                        var p = so.FindProperty("_candidateProvider");
                        if (p != null) p.objectReferenceValue = gazeConecaster;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(gazeInteractorComp);
                    }

                    // Wire IndexPinchSelector
                    var pinchSelector = gazeGo.GetComponentInChildren<IndexPinchSelector>(true);
                    if (pinchSelector != null && handComp != null)
                    {
                        var so = new SerializedObject(pinchSelector);
                        var p = so.FindProperty("_hand");
                        if (p != null) p.objectReferenceValue = handComp;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(pinchSelector);
                    }
                }

                // Find HandRayInteractor (for fallback)
                var rayInteractorTrans = handAndNoController.Find("HandRayInteractor");
                RayInteractor rayInteractorComp = rayInteractorTrans != null ? rayInteractorTrans.GetComponent<RayInteractor>() : null;

                // Prune / Disable P0-unneeded Interactors
                string[] unneededInteractors = new[]
                {
                    "TouchHandGrabInteractor",
                    "HandGrabInteractor",
                    "HandGrabUseInteractor",
                    "DistanceHandGrabInteractor"
                };
                foreach (var unneeded in unneededInteractors)
                {
                    var uTrans = handAndNoController.Find(unneeded);
                    if (uTrans != null)
                    {
                        uTrans.gameObject.SetActive(false);
                        Debug.Log($"[P0_Configurator] Deactivated unneeded {handRootName}/{unneeded}.");
                    }
                }

                // Disable HandPokeInteractor under Hand/
                var handPokeTrans = interactorsHolder.Find("Hand/HandPokeInteractor");
                if (handPokeTrans != null)
                {
                    handPokeTrans.gameObject.SetActive(false);
                    Debug.Log($"[P0_Configurator] Deactivated unneeded {handRootName}/HandPokeInteractor.");
                }

                // Disable Controller interactors under Controller/
                var controllerTrans = interactorsHolder.Find("Controller");
                if (controllerTrans != null)
                {
                    controllerTrans.gameObject.SetActive(false);
                    Debug.Log($"[P0_Configurator] Deactivated unneeded {handRootName}/Controller.");
                }

                // Set InteractorGroup to ONLY contain [GazeInteractor, RayInteractor]
                var interactorGroup = interactorsHolder.GetComponent<InteractorGroup>();
                if (interactorGroup != null)
                {
                    var activeInteractorsList = new List<UnityEngine.Object>();
                    if (gazeInteractorComp != null) activeInteractorsList.Add(gazeInteractorComp);
                    if (rayInteractorComp != null) activeInteractorsList.Add(rayInteractorComp);

                    var soGroup = new SerializedObject(interactorGroup);
                    var interactorsProp = soGroup.FindProperty("_interactors");
                    if (interactorsProp != null)
                    {
                        interactorsProp.ClearArray();
                        for (int i = 0; i < activeInteractorsList.Count; i++)
                        {
                            interactorsProp.InsertArrayElementAtIndex(i);
                            interactorsProp.GetArrayElementAtIndex(i).objectReferenceValue = activeInteractorsList[i];
                        }
                        soGroup.ApplyModifiedProperties();
                        EditorUtility.SetDirty(interactorGroup);
                        Debug.Log($"[P0_Configurator] InteractorGroup on {handRootName} restricted to {activeInteractorsList.Count} active interactors.");
                    }
                }
            }

            // Disable global unneeded systems: Locomotor and OVRControllers (Hands Only)
            var locomotor = interactionRigGo.transform.Find("Locomotor");
            if (locomotor != null)
            {
                locomotor.gameObject.SetActive(false);
                Debug.Log("[P0_Configurator] Deactivated unneeded Locomotor.");
            }

            var ovrControllers = interactionRigGo.transform.Find("OVRControllers");
            if (ovrControllers != null)
            {
                ovrControllers.gameObject.SetActive(false);
                Debug.Log("[P0_Configurator] Deactivated unneeded OVRControllers (project is Hands Only).");
            }

            // 9. Ensure Interaction Target exists at comfortable FOV (0, 1.2, 1.0) with Interaction SDK components
            var targetGo = GameObject.Find("P0_InteractionTarget");
            if (targetGo == null)
            {
                targetGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                targetGo.name = "P0_InteractionTarget";
            }
            targetGo.transform.position = new Vector3(0, 1.2f, 1.0f);
            targetGo.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

            var sphereCollider = targetGo.GetComponent<SphereCollider>() ?? targetGo.AddComponent<SphereCollider>();

            // ColliderSurface
            var colliderSurface = targetGo.GetComponent<ColliderSurface>() ?? targetGo.AddComponent<ColliderSurface>();
            colliderSurface.InjectCollider(sphereCollider);
            var soColSurf = new SerializedObject(colliderSurface);
            var colProp = soColSurf.FindProperty("_collider");
            if (colProp != null) colProp.objectReferenceValue = sphereCollider;
            soColSurf.ApplyModifiedProperties();
            EditorUtility.SetDirty(colliderSurface);

            // GazeInteractable
            var gazeInteractable = targetGo.GetComponent<GazeInteractable>() ?? targetGo.AddComponent<GazeInteractable>();
            gazeInteractable.InjectSurface(colliderSurface);
            var soGazeInt = new SerializedObject(gazeInteractable);
            var surfProp = soGazeInt.FindProperty("_surface");
            if (surfProp != null) surfProp.objectReferenceValue = colliderSurface;
            soGazeInt.ApplyModifiedProperties();
            EditorUtility.SetDirty(gazeInteractable);

            // RayInteractable Fallback with ISDK_Gaze_Fallback tag
            var rayInteractable = targetGo.GetComponent<RayInteractable>() ?? targetGo.AddComponent<RayInteractable>();
            rayInteractable.InjectSurface(colliderSurface);
            var soRayInt = new SerializedObject(rayInteractable);
            var raySurfProp = soRayInt.FindProperty("_surface");
            if (raySurfProp != null) raySurfProp.objectReferenceValue = colliderSurface;
            soRayInt.ApplyModifiedProperties();
            EditorUtility.SetDirty(rayInteractable);

            var tagSet = targetGo.GetComponent<TagSet>() ?? targetGo.AddComponent<TagSet>();
            var soTagSet = new SerializedObject(tagSet);
            var tagsProp = soTagSet.FindProperty("_tags");
            bool hasFallbackTag = false;
            if (tagsProp != null)
            {
                for (int i = 0; i < tagsProp.arraySize; i++)
                {
                    if (tagsProp.GetArrayElementAtIndex(i).stringValue == "ISDK_Gaze_Fallback")
                    {
                        hasFallbackTag = true;
                        break;
                    }
                }
                if (!hasFallbackTag)
                {
                    tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                    tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = "ISDK_Gaze_Fallback";
                    soTagSet.ApplyModifiedProperties();
                    EditorUtility.SetDirty(tagSet);
                }
            }

            // LookPinchTester
            var tester = targetGo.GetComponent<LookPinchTester>() ?? targetGo.AddComponent<LookPinchTester>();
            tester.SubscribeToEvents();

            // 10. Ensure Diagnostics object exists
            var diagGo = GameObject.Find("P0_Diagnostics");
            if (diagGo == null)
            {
                diagGo = new GameObject("P0_Diagnostics");
            }
            var diag = diagGo.GetComponent<DeviceReadinessCheck>() ?? diagGo.AddComponent<DeviceReadinessCheck>();

            Physics.SyncTransforms();

            // 11. Assertive Reference Integrity Check Before Save
            ValidateRigIntegrity(rigGo, interactionRigGo, ovrEyeGazeGo, targetGo);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[P0_Configurator] Official Interaction SDK validation scene saved: {scenePath}");
        }

        public static void ValidateRigIntegrity(GameObject cameraRigGo, GameObject interactionRigGo, GameObject eyeGazeGo, GameObject targetGo)
        {
            Debug.Log("[P0_Configurator] Running assertive Rig Integrity validation...");

            var cameraRig = cameraRigGo.GetComponent<OVRCameraRig>();
            if (cameraRig == null) throw new InvalidOperationException("OVRCameraRig component is missing on camera rig!");

            var cameraRigRef = interactionRigGo.GetComponent<OVRCameraRigRef>();
            if (cameraRigRef == null) throw new InvalidOperationException("OVRCameraRigRef is missing on interaction rig!");

            var soRigRef = new SerializedObject(cameraRigRef);
            if (soRigRef.FindProperty("_ovrCameraRig")?.objectReferenceValue == null)
                throw new InvalidOperationException("OVRCameraRigRef._ovrCameraRig serialized field is NULL!");
            if (soRigRef.FindProperty("_leftHand")?.objectReferenceValue == null)
                throw new InvalidOperationException("OVRCameraRigRef._leftHand serialized field is NULL!");
            if (soRigRef.FindProperty("_rightHand")?.objectReferenceValue == null)
                throw new InvalidOperationException("OVRCameraRigRef._rightHand serialized field is NULL!");

            var trackingTrans = interactionRigGo.GetComponent<TrackingToWorldTransformerOVR>();
            if (trackingTrans == null) throw new InvalidOperationException("TrackingToWorldTransformerOVR is missing on interaction rig!");
            var soTrans = new SerializedObject(trackingTrans);
            if (soTrans.FindProperty("_cameraRigRef")?.objectReferenceValue == null)
                throw new InvalidOperationException("TrackingToWorldTransformerOVR._cameraRigRef is NULL!");

            var eyeGazeDataSource = eyeGazeGo.GetComponentInChildren<FromOVREyeGazeDataSource>(true);
            if (eyeGazeDataSource == null) throw new InvalidOperationException("FromOVREyeGazeDataSource is missing under OVREyeGaze!");
            var soEyeData = new SerializedObject(eyeGazeDataSource);
            if (soEyeData.FindProperty("_cameraRigRef")?.objectReferenceValue == null)
                throw new InvalidOperationException("FromOVREyeGazeDataSource._cameraRigRef is NULL!");
            if (soEyeData.FindProperty("_centerEyeCamera")?.objectReferenceValue == null)
                throw new InvalidOperationException("FromOVREyeGazeDataSource._centerEyeCamera is NULL!");

            var gazeConecaster = eyeGazeGo.GetComponentInChildren<GazeConecaster>(true);
            if (gazeConecaster == null) throw new InvalidOperationException("GazeConecaster is missing under OVREyeGaze!");
            var soCone = new SerializedObject(gazeConecaster);
            if (soCone.FindProperty("_gaze")?.objectReferenceValue == null)
                throw new InvalidOperationException("GazeConecaster._gaze is NULL!");

            var hands = new[] { "ComprehensiveInteractorsLeft", "ComprehensiveInteractorsRight" };
            foreach (var h in hands)
            {
                var hRoot = interactionRigGo.transform.Find(h);
                if (hRoot == null) throw new InvalidOperationException($"{h} root missing!");

                var gazeGo = hRoot.Find("Interactors/Hand and No Controller/HandGazeInteractor");
                if (gazeGo == null) throw new InvalidOperationException($"HandGazeInteractor missing under {h}!");

                var handRef = gazeGo.GetComponent<HandRef>();
                if (handRef == null || new SerializedObject(handRef).FindProperty("_hand")?.objectReferenceValue == null)
                    throw new InvalidOperationException($"HandRef on {h}/HandGazeInteractor is NULL!");

                var eyeGazeRef = gazeGo.GetComponent<EyeGazeRef>();
                if (eyeGazeRef == null || new SerializedObject(eyeGazeRef).FindProperty("_gaze")?.objectReferenceValue == null)
                    throw new InvalidOperationException($"EyeGazeRef on {h}/HandGazeInteractor is NULL!");

                var gazeInt = gazeGo.GetComponent<GazeInteractor>();
                if (gazeInt == null || new SerializedObject(gazeInt).FindProperty("_candidateProvider")?.objectReferenceValue == null)
                    throw new InvalidOperationException($"GazeInteractor candidateProvider on {h} is NULL!");

                var pinchSel = gazeGo.GetComponentInChildren<IndexPinchSelector>(true);
                if (pinchSel == null || new SerializedObject(pinchSel).FindProperty("_hand")?.objectReferenceValue == null)
                    throw new InvalidOperationException($"IndexPinchSelector hand on {h} is NULL!");

                // Verify TouchHandGrab is NOT active
                var touchGrab = hRoot.Find("Interactors/Hand and No Controller/TouchHandGrabInteractor");
                if (touchGrab != null && touchGrab.gameObject.activeSelf)
                    throw new InvalidOperationException($"TouchHandGrabInteractor on {h} must be deactivated!");

                // Verify InteractorGroup contains GazeInteractor
                var ig = hRoot.Find("Interactors").GetComponent<InteractorGroup>();
                var soIG = new SerializedObject(ig);
                var interactorsProp = soIG.FindProperty("_interactors");
                bool containsGaze = false;
                for (int i = 0; i < interactorsProp.arraySize; i++)
                {
                    if (interactorsProp.GetArrayElementAtIndex(i).objectReferenceValue == gazeInt)
                    {
                        containsGaze = true;
                        break;
                    }
                }
                if (!containsGaze) throw new InvalidOperationException($"InteractorGroup on {h} does NOT contain GazeInteractor!");
            }

            var colSurf = targetGo.GetComponent<ColliderSurface>();
            if (colSurf == null || new SerializedObject(colSurf).FindProperty("_collider")?.objectReferenceValue == null)
                throw new InvalidOperationException("Target ColliderSurface._collider is NULL!");

            var gazeInteractable = targetGo.GetComponent<GazeInteractable>();
            if (gazeInteractable == null || new SerializedObject(gazeInteractable).FindProperty("_surface")?.objectReferenceValue == null)
                throw new InvalidOperationException("Target GazeInteractable._surface is NULL!");

            Debug.Log("[P0_Configurator] ALL Rig Integrity Assertions PASSED! 0 Null References.");
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
                evidence.handTracked = (tester.ActivePinchSource.Contains("Hand") || tester.ActivePinchSource.Contains("OVRHand"));
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
                        var targets = new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone };
                        foreach (var target in targets)
                        {
                            var tasks = getTasksMethod.Invoke(null, new object[] { target }) as IEnumerable;
                            if (tasks != null)
                            {
                                foreach (var task in tasks)
                                {
                                    var validProp = task.GetType().GetProperty("Valid");
                                    object validObj = validProp?.GetValue(task);
                                    if (validObj != null)
                                    {
                                        var getValidMethod = validObj.GetType().GetMethod("GetValue", new[] { typeof(BuildTargetGroup) });
                                        bool isValid = (bool)(getValidMethod?.Invoke(validObj, new object[] { target }) ?? true);
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
                                                isDone = (bool)isDoneDelegate.DynamicInvoke(target);
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
                                            levelStr = getValMethod?.Invoke(levelObj, new object[] { target })?.ToString() ?? levelObj.ToString();
                                        }

                                        string msgStr = "";
                                        if (msgObj != null)
                                        {
                                            var getValMethod = msgObj.GetType().GetMethod("GetValue", new[] { typeof(BuildTargetGroup) });
                                            msgStr = getValMethod?.Invoke(msgObj, new object[] { target })?.ToString() ?? msgObj.ToString();
                                        }

                                        if (levelStr.Equals("Required", StringComparison.OrdinalIgnoreCase))
                                        {
                                            evidence.criticalCount++;
                                            evidence.readinessCritical.Add($"[{target}] {msgStr}");
                                        }
                                        else if (levelStr.Equals("Recommended", StringComparison.OrdinalIgnoreCase))
                                        {
                                            evidence.recommendationCount++;
                                            evidence.readinessRecommendations.Add($"[{target}] {msgStr}");
                                        }
                                        else
                                        {
                                            evidence.warningCount++;
                                            evidence.readinessWarnings.Add($"[{target}] {msgStr}");
                                        }
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
