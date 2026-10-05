using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using Oculus.Interaction.Surfaces;
using Toss.Coin;

namespace Toss.Editor
{
    public static class P1_FingerPlaySetup
    {
        public const string ScenePath = "Assets/Scenes/P1_CoinPresence.unity";
        private const string FbxPath = "Assets/Meshes/Coin/TossCoin_Heads_Relief.fbx";
        private const string MatPath = "Assets/Materials/Coin/M_TossCoin_Presence.mat";

        private const string OvrCameraRigPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        private const string InteractionRigPrefabPath = "Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/OVRComprehensiveInteractionRig.prefab";
        private const string EyeGazePrefabPath = "Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/OVREyeGaze.prefab";
        private const string HandGazePrefabPath = "Packages/com.meta.xr.sdk.interaction/Runtime/Prefabs/Gaze/HandGazeInteractor.prefab";

        private struct PreFlightContext
        {
            public GameObject CoinGo;
            public GameObject RigGo;
            public GameObject InteractionRigGo;
            public Mesh ExpectedMesh;
            public Material ExpectedMaterial;
        }

        [MenuItem("Toss/P1.2 Setup Finger Play Scene")]
        public static void SetupFingerPlayScene()
        {
            Debug.Log("[P1.2 Setup] Starting Pre-Flight Checks for Finger Play Setup...");

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[P1.2 Setup] ABORTED: Editor is currently in play mode or transitioning.");
                return;
            }

            // 1. Dirty Scene Gate (BEFORE OpenScene): ensure no unsaved work is lost
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var loadedScene = SceneManager.GetSceneAt(i);
                if (loadedScene.isLoaded && loadedScene.isDirty)
                {
                    Debug.LogError($"[P1.2 Setup] ABORTED: Scene '{loadedScene.name}' has unsaved changes. Please save or discard current scene changes before running P1.2 Setup.");
                    return;
                }
            }

            // 2. Open scene fresh
            Scene scene;
            try
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[P1.2 Setup] ABORTED: Failed to open target scene {ScenePath}: {ex.Message}");
                return;
            }

            // 3. Inactive-inclusive preflight check & pre-mutation frozen asset validation
            if (!RunPreFlightChecks(scene, out PreFlightContext context, out string preflightError))
            {
                Debug.LogError($"[P1.2 Setup] ABORTED: Pre-flight check failed: {preflightError}");
                RollbackScene();
                return;
            }

            Debug.Log("[P1.2 Setup] Pre-flight checks PASSED. Configuring P1_CoinPresence scene for Finger Play interaction...");

            // 4. Execute configuration with cached references and rollback protection
            try
            {
                ExecuteSetup(scene, context);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[P1.2 Setup] EXCEPTION during setup: {ex.Message}\nStackTrace: {ex.StackTrace}");
                RollbackScene();
            }
        }

        private static void RollbackScene()
        {
            Debug.LogWarning("[P1.2 Setup] Rolling back scene to clean disk state. No modifications saved.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static bool RunPreFlightChecks(Scene scene, out PreFlightContext context, out string errorReason)
        {
            context = default;

            if (!File.Exists(ScenePath))
            {
                errorReason = $"Target scene file not found at {ScenePath}.";
                return false;
            }

            if (!File.Exists(FbxPath))
            {
                errorReason = $"Frozen Hero Coin FBX not found at {FbxPath}.";
                return false;
            }

            if (!File.Exists(MatPath))
            {
                errorReason = $"Frozen Hero Coin Material not found at {MatPath}.";
                return false;
            }

            // Verify and load frozen assets
            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbxAsset == null)
            {
                errorReason = $"Failed to load FBX asset at {FbxPath}.";
                return false;
            }
            var fbxMf = fbxAsset.GetComponentInChildren<MeshFilter>();
            if (fbxMf == null || fbxMf.sharedMesh == null)
            {
                errorReason = $"Frozen FBX at {FbxPath} does not contain a valid MeshFilter / sharedMesh.";
                return false;
            }
            context.ExpectedMesh = fbxMf.sharedMesh;

            context.ExpectedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (context.ExpectedMaterial == null)
            {
                errorReason = $"Failed to load Material asset at {MatPath}.";
                return false;
            }

            // Verify required Meta Interaction SDK prefabs
            string[] requiredPrefabs = {
                OvrCameraRigPrefabPath,
                InteractionRigPrefabPath,
                EyeGazePrefabPath,
                HandGazePrefabPath
            };

            foreach (var prefab in requiredPrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefab) == null)
                {
                    errorReason = $"Required Meta Interaction SDK prefab missing: {prefab}";
                    return false;
                }
            }

            // Inactive-inclusive uniqueness scan for scene objects
            var allCoinObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.name == "TossCoin_Presence" && !EditorUtility.IsPersistent(go) && go.scene == scene)
                .ToList();

            if (allCoinObjects.Count == 0)
            {
                errorReason = "TossCoin_Presence GameObject not found in scene.";
                return false;
            }

            if (allCoinObjects.Count > 1)
            {
                errorReason = $"Duplicate TossCoin_Presence found ({allCoinObjects.Count} instances). Uniqueness violation.";
                return false;
            }

            context.CoinGo = allCoinObjects[0];

            // Inactive-inclusive child uniqueness check
            int gazeChildren = 0;
            int handGrabChildren = 0;
            int distGrabChildren = 0;

            for (int i = 0; i < context.CoinGo.transform.childCount; i++)
            {
                var child = context.CoinGo.transform.GetChild(i);
                if (child.name == "ISDK_GazeInteraction") gazeChildren++;
                else if (child.name == "ISDK_HandGrabInteraction") handGrabChildren++;
                else if (child.name == "ISDK_DistanceHandGrabInteraction") distGrabChildren++;
            }

            if (gazeChildren > 1 || handGrabChildren > 1 || distGrabChildren > 1)
            {
                errorReason = $"Duplicate interaction children under TossCoin_Presence (Gaze: {gazeChildren}, HandGrab: {handGrabChildren}, DistanceGrab: {distGrabChildren}).";
                return false;
            }

            var rigs = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.name == "OVRCameraRig" && !EditorUtility.IsPersistent(go) && go.scene == scene)
                .ToList();

            if (rigs.Count > 1)
            {
                errorReason = $"Duplicate OVRCameraRig found ({rigs.Count} instances).";
                return false;
            }
            context.RigGo = rigs.Count == 1 ? rigs[0] : null;

            var interactionRigs = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.name == "OVRComprehensiveInteractionRig" && !EditorUtility.IsPersistent(go) && go.scene == scene)
                .ToList();

            if (interactionRigs.Count > 1)
            {
                errorReason = $"Duplicate OVRComprehensiveInteractionRig found ({interactionRigs.Count} instances).";
                return false;
            }
            context.InteractionRigGo = interactionRigs.Count == 1 ? interactionRigs[0] : null;

            // Pre-mutation frozen asset baseline validation on TossCoin_Presence
            var mf = context.CoinGo.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh != context.ExpectedMesh)
            {
                errorReason = $"Pre-mutation check failed: TossCoin_Presence MeshFilter.sharedMesh does not match frozen Hero Coin FBX mesh.";
                return false;
            }

            var mr = context.CoinGo.GetComponent<MeshRenderer>();
            if (mr == null || mr.sharedMaterial != context.ExpectedMaterial)
            {
                errorReason = $"Pre-mutation check failed: TossCoin_Presence MeshRenderer.sharedMaterial does not match frozen Satin Silver material.";
                return false;
            }

            var presenter = context.CoinGo.GetComponent<CoinPresenter>();
            if (presenter == null)
            {
                errorReason = "Pre-mutation check failed: CoinPresenter component missing on TossCoin_Presence.";
                return false;
            }

            var soPres = new SerializedObject(presenter);
            var customMeshProp = soPres.FindProperty("customReliefMesh");
            if (customMeshProp == null || customMeshProp.objectReferenceValue != context.ExpectedMesh)
            {
                errorReason = "Pre-mutation check failed: CoinPresenter customReliefMesh serialized property does not match frozen Hero Coin FBX mesh.";
                return false;
            }

            var useProcProp = soPres.FindProperty("useProceduralMesh");
            if (useProcProp == null || useProcProp.boolValue)
            {
                errorReason = "Pre-mutation check failed: CoinPresenter useProceduralMesh serialized property must be false.";
                return false;
            }

            if (context.CoinGo.transform.localScale != Vector3.one)
            {
                errorReason = $"Pre-mutation check failed: TossCoin_Presence transform.localScale must be (1,1,1), found {context.CoinGo.transform.localScale}.";
                return false;
            }

            errorReason = string.Empty;
            return true;
        }

        private static void ExecuteSetup(Scene scene, PreFlightContext context)
        {
            // 1. Ensure OVRCameraRig is properly configured (reusing discovered instance)
            var rigGo = context.RigGo;
            if (rigGo == null)
            {
                var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OvrCameraRigPrefabPath);
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
            rigGo.SetActive(true);

            rigGo.transform.position = Vector3.zero;
            rigGo.transform.rotation = Quaternion.identity;

            var ovrCameraRig = rigGo.GetComponent<OVRCameraRig>();
            if (ovrCameraRig == null) ovrCameraRig = rigGo.AddComponent<OVRCameraRig>();
            var ovrManager = rigGo.GetComponent<OVRManager>();
            if (ovrManager == null) ovrManager = rigGo.AddComponent<OVRManager>();
            ovrManager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;

            var centerEye = rigGo.transform.Find("TrackingSpace/CenterEyeAnchor");
            Camera centerCam = null;
            if (centerEye != null)
            {
                centerCam = centerEye.GetComponent<Camera>();
                if (centerCam == null) centerCam = centerEye.gameObject.AddComponent<Camera>();
                centerCam.tag = "MainCamera";
                centerCam.nearClipPlane = 0.05f;
                centerCam.farClipPlane = 100f;
                if (centerEye.GetComponent<AudioListener>() == null)
                {
                    centerEye.gameObject.AddComponent<AudioListener>();
                }
            }

            // Clean up standalone redundant cameras/listeners outside the rig
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam.gameObject.name == "Main Camera" && (cam.transform.parent == null || !cam.transform.IsChildOf(rigGo.transform)))
                {
                    UnityEngine.Object.DestroyImmediate(cam.gameObject);
                }
            }
            foreach (var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (centerEye != null && listener.transform == centerEye) continue;
                UnityEngine.Object.DestroyImmediate(listener);
            }

            // 2. Ensure OVRComprehensiveInteractionRig exists under OVRCameraRig (reusing discovered instance)
            var interactionRigGo = context.InteractionRigGo;
            if (interactionRigGo == null)
            {
                var interactionRigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InteractionRigPrefabPath);
                if (interactionRigPrefab != null)
                {
                    interactionRigGo = (GameObject)PrefabUtility.InstantiatePrefab(interactionRigPrefab, rigGo.transform);
                    interactionRigGo.name = "OVRComprehensiveInteractionRig";
                    Debug.Log("[P1.2 Setup] Instantiated OVRComprehensiveInteractionRig prefab.");
                }
            }
            if (interactionRigGo != null)
            {
                interactionRigGo.SetActive(true);
                if (interactionRigGo.transform.parent != rigGo.transform)
                {
                    interactionRigGo.transform.SetParent(rigGo.transform, false);
                }
            }

            // Deactivate systems that must not participate in P1.2 (Locomotor, OVRControllers)
            if (interactionRigGo != null)
            {
                var locomotor = interactionRigGo.transform.Find("Locomotor");
                if (locomotor != null)
                {
                    locomotor.gameObject.SetActive(false);
                    Debug.Log("[P1.2 Setup] Deactivated Locomotor under interaction rig.");
                }

                var ovrControllers = interactionRigGo.transform.Find("OVRControllers");
                if (ovrControllers != null)
                {
                    ovrControllers.gameObject.SetActive(false);
                    Debug.Log("[P1.2 Setup] Deactivated OVRControllers under interaction rig.");
                }
            }

            // Locate OVRHands
            OVRHand leftOvrHand = null;
            OVRHand rightOvrHand = null;
            var leftHandSource = interactionRigGo != null ? interactionRigGo.transform.Find("OVRHands/OVRHandDataSourceLeft") : null;
            if (leftHandSource != null) leftOvrHand = leftHandSource.GetComponent<OVRHand>();
            var rightHandSource = interactionRigGo != null ? interactionRigGo.transform.Find("OVRHands/OVRHandDataSourceRight") : null;
            if (rightHandSource != null) rightOvrHand = rightHandSource.GetComponent<OVRHand>();

            // Wire OVRCameraRigRef
            var cameraRigRef = interactionRigGo != null ? interactionRigGo.GetComponent<OVRCameraRigRef>() : null;
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
            }

            // Wire TrackingToWorldTransformerOVR
            var trackingTransformer = interactionRigGo != null ? interactionRigGo.GetComponent<TrackingToWorldTransformerOVR>() : null;
            if (trackingTransformer != null && cameraRigRef != null)
            {
                var soTrans = new SerializedObject(trackingTransformer);
                var refProp = soTrans.FindProperty("_cameraRigRef");
                if (refProp != null) refProp.objectReferenceValue = cameraRigRef;
                soTrans.ApplyModifiedProperties();
                EditorUtility.SetDirty(trackingTransformer);
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

            // 3. Ensure OVREyeGaze exists under TrackingSpace (P0 baseline preserved)
            var trackingSpace = rigGo.transform.Find("TrackingSpace");
            Transform eyeGazeParent = trackingSpace != null ? trackingSpace : rigGo.transform;
            var ovrEyeGazeGo = rigGo.transform.Find("TrackingSpace/OVREyeGaze")?.gameObject;
            if (ovrEyeGazeGo == null)
            {
                var eyeGazePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EyeGazePrefabPath);
                if (eyeGazePrefab != null)
                {
                    ovrEyeGazeGo = (GameObject)PrefabUtility.InstantiatePrefab(eyeGazePrefab, eyeGazeParent);
                    ovrEyeGazeGo.name = "OVREyeGaze";
                    Debug.Log("[P1.2 Setup] Instantiated OVREyeGaze prefab under TrackingSpace.");
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
                }

                gazeConecaster = ovrEyeGazeGo.GetComponentInChildren<GazeConecaster>(true);
                if (gazeConecaster == null && eyeGaze != null)
                {
                    var coneGo = new GameObject("GazeConecaster");
                    coneGo.transform.SetParent(eyeGaze.transform, false);
                    gazeConecaster = coneGo.AddComponent<GazeConecaster>();
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
                }
            }

            // 4. Configure Interactors on Left and Right hands (Enable HandGrab + DistanceHandGrab + HandGaze)
            var handGazePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandGazePrefabPath);
            var handSides = new[]
            {
                (name: "ComprehensiveInteractorsLeft", side: "Left"),
                (name: "ComprehensiveInteractorsRight", side: "Right")
            };

            foreach (var (handRootName, side) in handSides)
            {
                if (interactionRigGo == null) continue;
                var handRoot = interactionRigGo.transform.Find(handRootName);
                if (handRoot == null) continue;

                var handComp = handRoot.GetComponent<Oculus.Interaction.Input.Hand>();
                var interactorsHolder = handRoot.Find("Interactors");
                if (interactorsHolder == null) continue;

                var handAndNoController = interactorsHolder.Find("Hand and No Controller");
                if (handAndNoController == null) continue;

                // Wire / Ensure HandGazeInteractor
                var gazeInteractorTrans = handAndNoController.Find("HandGazeInteractor");
                GameObject gazeGo = gazeInteractorTrans != null ? gazeInteractorTrans.gameObject : null;
                if (gazeGo == null && handGazePrefab != null)
                {
                    gazeGo = (GameObject)PrefabUtility.InstantiatePrefab(handGazePrefab, handAndNoController);
                    gazeGo.name = "HandGazeInteractor";
                }
                GazeInteractor gazeInteractorComp = null;
                if (gazeGo != null)
                {
                    gazeGo.SetActive(true);
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

                // Activate HandGrabInteractor and DistanceHandGrabInteractor
                var handGrabTrans = handAndNoController.Find("HandGrabInteractor");
                if (handGrabTrans != null)
                {
                    handGrabTrans.gameObject.SetActive(true);
                    Debug.Log($"[P1.2 Setup] Activated {handRootName}/HandGrabInteractor.");
                }

                var distHandGrabTrans = handAndNoController.Find("DistanceHandGrabInteractor");
                if (distHandGrabTrans != null)
                {
                    distHandGrabTrans.gameObject.SetActive(true);
                    Debug.Log($"[P1.2 Setup] Activated {handRootName}/DistanceHandGrabInteractor.");
                }

                var rayInteractorTrans = handAndNoController.Find("HandRayInteractor");
                if (rayInteractorTrans != null)
                {
                    rayInteractorTrans.gameObject.SetActive(true);
                }

                // Deactivate rogue interactors on hand (poke, touch grab) to keep active interactor set strictly P1.2
                var touchGrabTrans = handAndNoController.Find("TouchHandGrabInteractor");
                if (touchGrabTrans != null) touchGrabTrans.gameObject.SetActive(false);

                var pokeTrans = handAndNoController.Find("HandPokeInteractor");
                if (pokeTrans == null) pokeTrans = handAndNoController.Find("PokeInteractor");
                if (pokeTrans != null) pokeTrans.gameObject.SetActive(false);

                // Deactivate controller branch if present under interactors
                var controllerHolder = interactorsHolder.Find("Controller and No Hand");
                if (controllerHolder != null) controllerHolder.gameObject.SetActive(false);

                // Update InteractorGroup to prioritize HandGrab > DistanceHandGrab > HandGaze > HandRay
                var interactorGroup = interactorsHolder.GetComponent<InteractorGroup>();
                if (interactorGroup != null)
                {
                    var activeInteractors = new List<UnityEngine.Object>();
                    if (handGrabTrans != null && handGrabTrans.GetComponent<HandGrabInteractor>() != null)
                        activeInteractors.Add(handGrabTrans.GetComponent<HandGrabInteractor>());
                    if (distHandGrabTrans != null && distHandGrabTrans.GetComponent<DistanceHandGrabInteractor>() != null)
                        activeInteractors.Add(distHandGrabTrans.GetComponent<DistanceHandGrabInteractor>());
                    if (gazeInteractorComp != null)
                        activeInteractors.Add(gazeInteractorComp);
                    if (rayInteractorTrans != null && rayInteractorTrans.GetComponent<RayInteractor>() != null)
                        activeInteractors.Add(rayInteractorTrans.GetComponent<RayInteractor>());

                    var soGroup = new SerializedObject(interactorGroup);
                    var interactorsProp = soGroup.FindProperty("_interactors");
                    if (interactorsProp != null)
                    {
                        interactorsProp.ClearArray();
                        for (int i = 0; i < activeInteractors.Count; i++)
                        {
                            interactorsProp.InsertArrayElementAtIndex(i);
                            interactorsProp.GetArrayElementAtIndex(i).objectReferenceValue = activeInteractors[i];
                        }
                        soGroup.ApplyModifiedProperties();
                        EditorUtility.SetDirty(interactorGroup);
                        Debug.Log($"[P1.2 Setup] Wired InteractorGroup on {handRootName} with {activeInteractors.Count} interactors.");
                    }
                }
            }

            // 5. Configure TossCoin_Presence for Grabbing and FingerPlay (reusing cached CoinGo)
            var coinGo = context.CoinGo;
            if (coinGo == null)
            {
                throw new InvalidOperationException("TossCoin_Presence not found in scene context!");
            }

            // Ensure frozen visual asset & exact binding
            var mf = coinGo.GetComponent<MeshFilter>();
            if (mf == null) mf = coinGo.AddComponent<MeshFilter>();
            var mr = coinGo.GetComponent<MeshRenderer>();
            if (mr == null) mr = coinGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = context.ExpectedMesh;
            mr.sharedMaterial = context.ExpectedMaterial;
            coinGo.transform.localScale = Vector3.one;

            // Ensure CoinPresenter is strictly bound to frozen visual asset
            var presenter = coinGo.GetComponent<CoinPresenter>();
            if (presenter == null) presenter = coinGo.AddComponent<CoinPresenter>();
            var soPresenter = new SerializedObject(presenter);
            soPresenter.FindProperty("customReliefMesh").objectReferenceValue = context.ExpectedMesh;
            soPresenter.FindProperty("useProceduralMesh").boolValue = false;
            soPresenter.ApplyModifiedProperties();
            EditorUtility.SetDirty(presenter);

            // Step 1: Ensure Rigidbody FIRST before any interaction components
            var rb = coinGo.GetComponent<Rigidbody>();
            if (rb == null) rb = coinGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            EditorUtility.SetDirty(rb);

            // Step 2: Ensure SphereCollider: Trigger (radius 0.02m = 40mm diameter)
            var col = coinGo.GetComponent<SphereCollider>();
            if (col == null) col = coinGo.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.02f;
            col.center = Vector3.zero;
            EditorUtility.SetDirty(col);

            // Step 3: Ensure GrabFreeTransformer (before Grabbable wires it)
            var transformer = coinGo.GetComponent<GrabFreeTransformer>();
            if (transformer == null) transformer = coinGo.AddComponent<GrabFreeTransformer>();
            var soTransf = new SerializedObject(transformer);
            // Constrain scale to 1.0 (lock scale factor to eliminate sudden scale change)
            var scaleProp = soTransf.FindProperty("_scaleConstraints");
            if (scaleProp != null)
            {
                var relProp = scaleProp.FindPropertyRelative("ConstraintsAreRelative");
                if (relProp != null) relProp.boolValue = true;
                var xProp = scaleProp.FindPropertyRelative("XAxis");
                if (xProp != null)
                {
                    xProp.FindPropertyRelative("ConstrainAxis").boolValue = true;
                    xProp.FindPropertyRelative("AxisRange.Min").floatValue = 1f;
                    xProp.FindPropertyRelative("AxisRange.Max").floatValue = 1f;
                }
                var yProp = scaleProp.FindPropertyRelative("YAxis");
                if (yProp != null)
                {
                    yProp.FindPropertyRelative("ConstrainAxis").boolValue = true;
                    yProp.FindPropertyRelative("AxisRange.Min").floatValue = 1f;
                    yProp.FindPropertyRelative("AxisRange.Max").floatValue = 1f;
                }
                var zProp = scaleProp.FindPropertyRelative("ZAxis");
                if (zProp != null)
                {
                    zProp.FindPropertyRelative("ConstrainAxis").boolValue = true;
                    zProp.FindPropertyRelative("AxisRange.Min").floatValue = 1f;
                    zProp.FindPropertyRelative("AxisRange.Max").floatValue = 1f;
                }
            }
            soTransf.ApplyModifiedProperties();
            EditorUtility.SetDirty(transformer);

            // Step 4: Ensure Grabbable: Single-owner selection constraints, bind Rigidbody and Transformer
            var grabbable = coinGo.GetComponent<Grabbable>();
            if (grabbable == null) grabbable = coinGo.AddComponent<Grabbable>();
            var soGrabbable = new SerializedObject(grabbable);
            soGrabbable.FindProperty("_rigidbody").objectReferenceValue = rb;
            soGrabbable.FindProperty("_targetTransform").objectReferenceValue = coinGo.transform;
            soGrabbable.FindProperty("_kinematicWhileSelected").boolValue = true;
            soGrabbable.FindProperty("_throwWhenUnselected").boolValue = false;
            soGrabbable.FindProperty("_maxGrabPoints").intValue = 1;
            soGrabbable.FindProperty("_transferOnSecondSelection").boolValue = false;
            soGrabbable.FindProperty("_oneGrabTransformer").objectReferenceValue = transformer;
            soGrabbable.ApplyModifiedProperties();
            EditorUtility.SetDirty(grabbable);

            // Step 5: Configure FingerPlayHarness on TossCoin_Presence (now that Rigidbody and Grabbable exist)
            var harness = coinGo.GetComponent<FingerPlayHarness>();
            if (harness == null) harness = coinGo.AddComponent<FingerPlayHarness>();
            var soHarness = new SerializedObject(harness);
            var hGrabbableProp = soHarness.FindProperty("_grabbable");
            if (hGrabbableProp != null) hGrabbableProp.objectReferenceValue = grabbable;
            var hPresProp = soHarness.FindProperty("_coinPresenter");
            if (hPresProp != null) hPresProp.objectReferenceValue = presenter;
            var hRbProp = soHarness.FindProperty("_rigidbody");
            if (hRbProp != null) hRbProp.objectReferenceValue = rb;
            soHarness.ApplyModifiedProperties();
            EditorUtility.SetDirty(harness);

            // Step 6: Configure Child ISDK_HandGrabInteraction
            var handGrabChild = coinGo.transform.Find("ISDK_HandGrabInteraction");
            if (handGrabChild == null)
            {
                var childGo = new GameObject("ISDK_HandGrabInteraction");
                childGo.transform.SetParent(coinGo.transform, false);
                handGrabChild = childGo.transform;
            }
            var handGrabInteractable = handGrabChild.GetComponent<HandGrabInteractable>();
            if (handGrabInteractable == null) handGrabInteractable = handGrabChild.gameObject.AddComponent<HandGrabInteractable>();
            var soHgi = new SerializedObject(handGrabInteractable);
            soHgi.FindProperty("_rigidbody").objectReferenceValue = rb;
            soHgi.FindProperty("_pointableElement").objectReferenceValue = grabbable;
            soHgi.FindProperty("_supportedGrabTypes").intValue = (int)GrabTypeFlags.All;
            soHgi.FindProperty("_resetGrabOnGrabsUpdated").boolValue = true;
            soHgi.FindProperty("_maxSelectingInteractors").intValue = 1;
            var hgiFilters = soHgi.FindProperty("_interactorFilters");
            if (hgiFilters != null)
            {
                hgiFilters.ClearArray();
                hgiFilters.InsertArrayElementAtIndex(0);
                hgiFilters.GetArrayElementAtIndex(0).objectReferenceValue = harness;
            }
            soHgi.ApplyModifiedProperties();
            EditorUtility.SetDirty(handGrabInteractable);

            // Step 7: Configure Child ISDK_DistanceHandGrabInteraction (with MoveAtSourceProvider)
            var distGrabChild = coinGo.transform.Find("ISDK_DistanceHandGrabInteraction");
            if (distGrabChild == null)
            {
                var childGo = new GameObject("ISDK_DistanceHandGrabInteraction");
                childGo.transform.SetParent(coinGo.transform, false);
                distGrabChild = childGo.transform;
            }
            var distMoveAtSource = distGrabChild.GetComponent<MoveAtSourceProvider>();
            if (distMoveAtSource == null) distMoveAtSource = distGrabChild.gameObject.AddComponent<MoveAtSourceProvider>();
            var distGrabInteractable = distGrabChild.GetComponent<DistanceHandGrabInteractable>();
            if (distGrabInteractable == null) distGrabInteractable = distGrabChild.gameObject.AddComponent<DistanceHandGrabInteractable>();
            var soDgi = new SerializedObject(distGrabInteractable);
            soDgi.FindProperty("_rigidbody").objectReferenceValue = rb;
            soDgi.FindProperty("_pointableElement").objectReferenceValue = grabbable;
            soDgi.FindProperty("_movementProvider").objectReferenceValue = distMoveAtSource;
            soDgi.FindProperty("_supportedGrabTypes").intValue = (int)GrabTypeFlags.Pinch;
            soDgi.FindProperty("_resetGrabOnGrabsUpdated").boolValue = true;
            soDgi.FindProperty("_maxSelectingInteractors").intValue = 1;
            var dgiFilters = soDgi.FindProperty("_interactorFilters");
            if (dgiFilters != null)
            {
                dgiFilters.ClearArray();
                dgiFilters.InsertArrayElementAtIndex(0);
                dgiFilters.GetArrayElementAtIndex(0).objectReferenceValue = harness;
            }
            soDgi.ApplyModifiedProperties();
            EditorUtility.SetDirty(distGrabInteractable);

            // Step 8: Configure Child ISDK_GazeInteraction (with MoveAtSourceProvider for Hand-based motion authority)
            var gazeChild = coinGo.transform.Find("ISDK_GazeInteraction");
            if (gazeChild == null)
            {
                var childGo = new GameObject("ISDK_GazeInteraction");
                childGo.transform.SetParent(coinGo.transform, false);
                gazeChild = childGo.transform;
            }
            var colliderSurface = gazeChild.GetComponent<ColliderSurface>();
            if (colliderSurface == null) colliderSurface = gazeChild.gameObject.AddComponent<ColliderSurface>();
            var soCs = new SerializedObject(colliderSurface);
            var colProp = soCs.FindProperty("_collider");
            if (colProp != null) colProp.objectReferenceValue = col;
            soCs.ApplyModifiedProperties();
            EditorUtility.SetDirty(colliderSurface);

            var gazeMoveAtSource = gazeChild.GetComponent<MoveAtSourceProvider>();
            if (gazeMoveAtSource == null) gazeMoveAtSource = gazeChild.gameObject.AddComponent<MoveAtSourceProvider>();
            var gazeInteractable = gazeChild.GetComponent<GazeInteractable>();
            if (gazeInteractable == null) gazeInteractable = gazeChild.gameObject.AddComponent<GazeInteractable>();
            var soGi = new SerializedObject(gazeInteractable);
            var surfProp = soGi.FindProperty("_surface");
            if (surfProp != null) surfProp.objectReferenceValue = colliderSurface;
            var peProp = soGi.FindProperty("_pointableElement");
            if (peProp != null) peProp.objectReferenceValue = grabbable;
            var moveProp = soGi.FindProperty("_movementProvider");
            if (moveProp != null) moveProp.objectReferenceValue = gazeMoveAtSource;
            var multiProp = soGi.FindProperty("_multiGrabScalingSupport");
            if (multiProp != null) multiProp.boolValue = false;
            soGi.FindProperty("_maxSelectingInteractors").intValue = 1;
            var giFilters = soGi.FindProperty("_interactorFilters");
            if (giFilters != null)
            {
                giFilters.ClearArray();
                giFilters.InsertArrayElementAtIndex(0);
                giFilters.GetArrayElementAtIndex(0).objectReferenceValue = harness;
            }
            soGi.ApplyModifiedProperties();
            EditorUtility.SetDirty(gazeInteractable);

            // 6. Pre-Save Scene Validation (Multi-factor exact check via SerializedObject)
            if (!ValidateFingerPlayScene(coinGo, interactionRigGo, rigGo, context.ExpectedMesh, context.ExpectedMaterial, out List<string> validationErrors))
            {
                Debug.LogError($"[P1.2 Setup] VALIDATION FAILED with {validationErrors.Count} error(s)! Rolling back scene...");
                foreach (var err in validationErrors)
                {
                    Debug.LogError($"  - {err}");
                }
                RollbackScene();
                return;
            }

            // Save scene and assets only upon 100% validation success
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[P1.2 Setup] Finger Play Scene configured, validated, and saved successfully!");
            AuditFingerPlayScene();
        }

        private static bool ValidateFingerPlayScene(GameObject coinGo, GameObject interactionRigGo, GameObject rigGo, Mesh expectedMesh, Material expectedMat, out List<string> errors)
        {
            errors = new List<string>();

            if (coinGo == null)
            {
                errors.Add("TossCoin_Presence GameObject is missing.");
                return false;
            }

            // 1. Frozen visual validation
            var mf = coinGo.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) errors.Add("MeshFilter missing or sharedMesh is null.");
            else if (mf.sharedMesh != expectedMesh) errors.Add("MeshFilter.sharedMesh does not match frozen Hero Coin FBX mesh.");

            var mr = coinGo.GetComponent<MeshRenderer>();
            if (mr == null || mr.sharedMaterial == null) errors.Add("MeshRenderer missing or sharedMaterial is null.");
            else if (mr.sharedMaterial != expectedMat) errors.Add("MeshRenderer.sharedMaterial does not match frozen Satin Silver material.");

            var presenter = coinGo.GetComponent<CoinPresenter>();
            if (presenter == null) errors.Add("CoinPresenter missing on coin.");
            else
            {
                var soPres = new SerializedObject(presenter);
                var cMeshProp = soPres.FindProperty("customReliefMesh");
                if (cMeshProp == null || cMeshProp.objectReferenceValue != expectedMesh)
                    errors.Add("CoinPresenter.customReliefMesh serialized property does not match frozen Hero Coin FBX mesh.");
                var procProp = soPres.FindProperty("useProceduralMesh");
                if (procProp == null || procProp.boolValue)
                    errors.Add("CoinPresenter.useProceduralMesh serialized property must be false.");
            }

            if (coinGo.transform.localScale != Vector3.one) errors.Add($"Coin root scale must be (1,1,1), found {coinGo.transform.localScale}.");

            // 2. Physics & Collider
            var rb = coinGo.GetComponent<Rigidbody>();
            if (rb == null) errors.Add("Coin Rigidbody is missing.");
            else
            {
                if (!rb.isKinematic) errors.Add("Coin Rigidbody must be isKinematic = true.");
                if (rb.useGravity) errors.Add("Coin Rigidbody must be useGravity = false.");
            }

            var col = coinGo.GetComponent<SphereCollider>();
            if (col == null) errors.Add("Coin SphereCollider is missing.");
            else
            {
                if (!col.isTrigger) errors.Add("Coin SphereCollider must be isTrigger = true.");
                if (Mathf.Abs(col.radius - 0.02f) > 0.005f) errors.Add($"Coin SphereCollider radius must be ~0.02m (found {col.radius}).");
            }

            // 3. Grabbable (SerializedObject inspection)
            var grabbable = coinGo.GetComponent<Grabbable>();
            if (grabbable == null) errors.Add("Coin Grabbable component is missing.");
            else
            {
                var soGrab = new SerializedObject(grabbable);
                var rbProp = soGrab.FindProperty("_rigidbody");
                if (rbProp == null || rbProp.objectReferenceValue != rb) errors.Add("Grabbable._rigidbody does not point to coin Rigidbody.");
                var targetProp = soGrab.FindProperty("_targetTransform");
                if (targetProp == null || targetProp.objectReferenceValue != coinGo.transform) errors.Add("Grabbable._targetTransform does not point to coin transform.");
                var maxGrabProp = soGrab.FindProperty("_maxGrabPoints");
                if (maxGrabProp == null || maxGrabProp.intValue != 1) errors.Add("Grabbable._maxGrabPoints must be 1.");
                var kinProp = soGrab.FindProperty("_kinematicWhileSelected");
                if (kinProp == null || !kinProp.boolValue) errors.Add("Grabbable._kinematicWhileSelected must be true.");
                var throwProp = soGrab.FindProperty("_throwWhenUnselected");
                if (throwProp == null || throwProp.boolValue) errors.Add("Grabbable._throwWhenUnselected must be false.");
                var transfProp = soGrab.FindProperty("_oneGrabTransformer");
                if (transfProp == null || transfProp.objectReferenceValue == null) errors.Add("Grabbable._oneGrabTransformer must not be null.");
            }

            // 4. GrabFreeTransformer (SerializedObject inspection)
            var transformer = coinGo.GetComponent<GrabFreeTransformer>();
            if (transformer == null) errors.Add("Coin GrabFreeTransformer is missing.");
            else
            {
                var soTransf = new SerializedObject(transformer);
                var scaleProp = soTransf.FindProperty("_scaleConstraints");
                if (scaleProp == null) errors.Add("GrabFreeTransformer._scaleConstraints property missing.");
                else
                {
                    var relProp = scaleProp.FindPropertyRelative("ConstraintsAreRelative");
                    if (relProp == null || !relProp.boolValue) errors.Add("GrabFreeTransformer scale constraints must be relative.");
                }
            }

            // 5. FingerPlayHarness (SerializedObject inspection)
            var harness = coinGo.GetComponent<FingerPlayHarness>();
            if (harness == null) errors.Add("FingerPlayHarness is missing on coin.");
            else
            {
                var soHarness = new SerializedObject(harness);
                var grabProp = soHarness.FindProperty("_grabbable");
                if (grabProp == null || grabProp.objectReferenceValue != grabbable) errors.Add("FingerPlayHarness._grabbable does not point to coin Grabbable.");
                var presProp = soHarness.FindProperty("_coinPresenter");
                if (presProp == null || presProp.objectReferenceValue != presenter) errors.Add("FingerPlayHarness._coinPresenter does not point to CoinPresenter.");
                var rbHarnessProp = soHarness.FindProperty("_rigidbody");
                if (rbHarnessProp == null || rbHarnessProp.objectReferenceValue != rb) errors.Add("FingerPlayHarness._rigidbody does not point to coin Rigidbody.");
            }

            // 6. Child Interactables (SerializedObject inspection)
            var hgi = coinGo.GetComponentInChildren<HandGrabInteractable>(true);
            if (hgi == null) errors.Add("ISDK_HandGrabInteraction (HandGrabInteractable) is missing.");
            else
            {
                var soHgi = new SerializedObject(hgi);
                var peProp = soHgi.FindProperty("_pointableElement");
                if (peProp == null || peProp.objectReferenceValue != grabbable) errors.Add("HandGrabInteractable._pointableElement does not point to coin Grabbable.");
                var maxSelProp = soHgi.FindProperty("_maxSelectingInteractors");
                if (maxSelProp == null || maxSelProp.intValue != 1) errors.Add("HandGrabInteractable._maxSelectingInteractors must be 1.");
                var filterProp = soHgi.FindProperty("_interactorFilters");
                if (filterProp == null || filterProp.arraySize == 0 || filterProp.GetArrayElementAtIndex(0).objectReferenceValue != harness)
                    errors.Add("HandGrabInteractable._interactorFilters must contain FingerPlayHarness.");
            }

            var dgi = coinGo.GetComponentInChildren<DistanceHandGrabInteractable>(true);
            if (dgi == null) errors.Add("ISDK_DistanceHandGrabInteraction (DistanceHandGrabInteractable) is missing.");
            else
            {
                var soDgi = new SerializedObject(dgi);
                var peProp = soDgi.FindProperty("_pointableElement");
                if (peProp == null || peProp.objectReferenceValue != grabbable) errors.Add("DistanceHandGrabInteractable._pointableElement does not point to coin Grabbable.");
                var moveProp = soDgi.FindProperty("_movementProvider");
                if (moveProp == null || moveProp.objectReferenceValue == null) errors.Add("DistanceHandGrabInteractable._movementProvider must not be null.");
                var maxSelProp = soDgi.FindProperty("_maxSelectingInteractors");
                if (maxSelProp == null || maxSelProp.intValue != 1) errors.Add("DistanceHandGrabInteractable._maxSelectingInteractors must be 1.");
                var filterProp = soDgi.FindProperty("_interactorFilters");
                if (filterProp == null || filterProp.arraySize == 0 || filterProp.GetArrayElementAtIndex(0).objectReferenceValue != harness)
                    errors.Add("DistanceHandGrabInteractable._interactorFilters must contain FingerPlayHarness.");
            }

            var gi = coinGo.GetComponentInChildren<GazeInteractable>(true);
            if (gi == null) errors.Add("ISDK_GazeInteraction (GazeInteractable) is missing.");
            else
            {
                var soGi = new SerializedObject(gi);
                var peProp = soGi.FindProperty("_pointableElement");
                if (peProp == null || peProp.objectReferenceValue != grabbable) errors.Add("GazeInteractable._pointableElement does not point to coin Grabbable.");
                var surfProp = soGi.FindProperty("_surface");
                if (surfProp == null || surfProp.objectReferenceValue == null) errors.Add("GazeInteractable._surface must not be null.");
                var moveProp = soGi.FindProperty("_movementProvider");
                if (moveProp == null || moveProp.objectReferenceValue == null) errors.Add("GazeInteractable._movementProvider must not be null (motion must be hand-driven).");
                var maxSelProp = soGi.FindProperty("_maxSelectingInteractors");
                if (maxSelProp == null || maxSelProp.intValue != 1) errors.Add("GazeInteractable._maxSelectingInteractors must be 1.");
                var filterProp = soGi.FindProperty("_interactorFilters");
                if (filterProp == null || filterProp.arraySize == 0 || filterProp.GetArrayElementAtIndex(0).objectReferenceValue != harness)
                    errors.Add("GazeInteractable._interactorFilters must contain FingerPlayHarness.");
            }

            var cs = coinGo.GetComponentInChildren<ColliderSurface>(true);
            if (cs == null) errors.Add("ISDK_GazeInteraction (ColliderSurface) is missing.");
            else
            {
                var soCs = new SerializedObject(cs);
                var colProp = soCs.FindProperty("_collider");
                if (colProp == null || colProp.objectReferenceValue != col) errors.Add("ColliderSurface._collider does not point to coin SphereCollider.");
            }

            // 7. Rig validation
            if (rigGo == null) errors.Add("OVRCameraRig is missing.");
            if (interactionRigGo == null) errors.Add("OVRComprehensiveInteractionRig is missing.");

            if (interactionRigGo != null)
            {
                var locomotor = interactionRigGo.transform.Find("Locomotor");
                if (locomotor != null && locomotor.gameObject.activeSelf)
                    errors.Add("Locomotor under interaction rig must be deactivated.");

                var ovrControllers = interactionRigGo.transform.Find("OVRControllers");
                if (ovrControllers != null && ovrControllers.gameObject.activeSelf)
                    errors.Add("OVRControllers under interaction rig must be deactivated.");

                var handSides = new[] { "ComprehensiveInteractorsLeft", "ComprehensiveInteractorsRight" };
                foreach (var handSide in handSides)
                {
                    var handRoot = interactionRigGo.transform.Find(handSide);
                    if (handRoot != null)
                    {
                        var handAndNoController = handRoot.Find("Interactors/Hand and No Controller");
                        if (handAndNoController != null)
                        {
                            var hg = handAndNoController.Find("HandGrabInteractor");
                            if (hg == null || !hg.gameObject.activeSelf) errors.Add($"{handSide}/HandGrabInteractor must be active.");

                            var dhg = handAndNoController.Find("DistanceHandGrabInteractor");
                            if (dhg == null || !dhg.gameObject.activeSelf) errors.Add($"{handSide}/DistanceHandGrabInteractor must be active.");

                            var hgaze = handAndNoController.Find("HandGazeInteractor");
                            if (hgaze == null || !hgaze.gameObject.activeSelf) errors.Add($"{handSide}/HandGazeInteractor must be active.");

                            var touchGrab = handAndNoController.Find("TouchHandGrabInteractor");
                            if (touchGrab != null && touchGrab.gameObject.activeSelf) errors.Add($"{handSide}/TouchHandGrabInteractor must be deactivated.");

                            var poke = handAndNoController.Find("HandPokeInteractor");
                            if (poke == null) poke = handAndNoController.Find("PokeInteractor");
                            if (poke != null && poke.gameObject.activeSelf) errors.Add($"{handSide}/HandPokeInteractor must be deactivated.");
                        }

                        var controllerHolder = handRoot.Find("Interactors/Controller and No Hand");
                        if (controllerHolder != null && controllerHolder.gameObject.activeSelf)
                            errors.Add($"{handSide}/Controller and No Hand must be deactivated.");
                    }
                }
            }

            return errors.Count == 0;
        }

        [MenuItem("Toss/P1.2 Audit Finger Play Scene")]
        public static void AuditFingerPlayScene()
        {
            Debug.Log("[P1.2 Audit] Auditing P1_CoinPresence scene...");

            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"[P1.2 Audit] Scene {ScenePath} not found!");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var coinGo = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(go => go.name == "TossCoin_Presence" && !EditorUtility.IsPersistent(go) && go.scene == scene);
            if (coinGo == null)
            {
                Debug.LogError("[P1.2 Audit] TossCoin_Presence missing!");
                return;
            }

            var rb = coinGo.GetComponent<Rigidbody>();
            var col = coinGo.GetComponent<SphereCollider>();
            var grabbable = coinGo.GetComponent<Grabbable>();
            var presenter = coinGo.GetComponent<CoinPresenter>();
            var harness = coinGo.GetComponent<FingerPlayHarness>();
            var transformer = coinGo.GetComponent<GrabFreeTransformer>();

            var hgi = coinGo.GetComponentInChildren<HandGrabInteractable>(true);
            var dgi = coinGo.GetComponentInChildren<DistanceHandGrabInteractable>(true);
            var gi = coinGo.GetComponentInChildren<GazeInteractable>(true);
            var cs = coinGo.GetComponentInChildren<ColliderSurface>(true);

            var interactionRig = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(go => go.name == "OVRComprehensiveInteractionRig" && !EditorUtility.IsPersistent(go) && go.scene == scene);

            bool dgiHasMove = false;
            if (dgi != null)
            {
                var soDgi = new SerializedObject(dgi);
                dgiHasMove = soDgi.FindProperty("_movementProvider")?.objectReferenceValue != null;
            }

            bool giHasMove = false;
            if (gi != null)
            {
                var soGi = new SerializedObject(gi);
                giHasMove = soGi.FindProperty("_movementProvider")?.objectReferenceValue != null;
            }

            Debug.Log($"[P1.2 Audit] Coin Rigidbody: {(rb != null ? "PASS (isKinematic=" + rb.isKinematic + ")" : "FAIL")}");
            Debug.Log($"[P1.2 Audit] Coin Collider: {(col != null ? "PASS (isTrigger=" + col.isTrigger + ", r=" + col.radius + ")" : "FAIL")}");
            Debug.Log($"[P1.2 Audit] Grabbable: {(grabbable != null ? "PASS (maxPoints=" + grabbable.MaxGrabPoints + ")" : "FAIL")}");
            Debug.Log($"[P1.2 Audit] GrabFreeTransformer: {(transformer != null ? "PASS" : "FAIL")}");
            Debug.Log($"[P1.2 Audit] HandGrabInteractable: {(hgi != null ? "PASS" : "FAIL")}");
            Debug.Log($"[P1.2 Audit] DistanceHandGrabInteractable: {(dgi != null ? "PASS (movementProvider=" + dgiHasMove + ")" : "FAIL")}");
            Debug.Log($"[P1.2 Audit] GazeInteractable: {(gi != null ? "PASS (movementProvider=" + giHasMove + ")" : "FAIL")}");
            Debug.Log($"[P1.2 Audit] ColliderSurface: {(cs != null ? "PASS" : "FAIL")}");
            Debug.Log($"[P1.2 Audit] FingerPlayHarness: {(harness != null ? "PASS" : "FAIL")}");
            Debug.Log($"[P1.2 Audit] CoinPresenter: {(presenter != null ? "PASS" : "FAIL")}");
            Debug.Log($"[P1.2 Audit] OVRComprehensiveInteractionRig: {(interactionRig != null ? "PASS" : "FAIL")}");
        }
    }
}
