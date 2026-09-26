using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR;
using Toss.Interaction;

namespace Toss.Diagnostics
{
    [Serializable]
    public class DeviceReadinessReport
    {
        public string timestamp;
        public string unityVersion;
        public string targetPlatform;
        public bool isXrActive;
        public string xrLoadedModel;
        public string openXrRuntimeEnvironment;
        public string simulatorConfiguredProfile;
        public string simulatorActiveSessionProfile;

        [Header("Configuration Checks")]
        public string configuredTargetDevices;
        public string configuredHandTrackingSupport;
        public bool controllerFreeConfigured;

        [Header("Runtime XR Input Evidence")]
        public bool gazeTargetEntered;
        public string gazeSource;
        public int gazeEnterCount;
        public bool pinchObserved;
        public string pinchSource;
        public int pinchStartCount;
        public bool lookAndPinchTriggered;
        public int lookAndPinchTriggerCount;

        [Header("Meta Project Setup Audit")]
        public int criticalCount;
        public List<string> criticalMessages = new List<string>();
        public int warningCount;
        public List<string> warningMessages = new List<string>();
        public int recommendationCount;
        public List<string> recommendationMessages = new List<string>();

        public List<string> inputDevices = new List<string>();
        public List<string> notes = new List<string>();
    }

    [DefaultExecutionOrder(-100)]
    public class DeviceReadinessCheck : MonoBehaviour
    {
        [SerializeField] private string reportFileName = "DeviceReadinessReport.json";

        public DeviceReadinessReport CurrentReport { get; private set; }

        private void Start()
        {
            RunReadinessCheck();
        }

        public DeviceReadinessReport RunReadinessCheck()
        {
            var report = new DeviceReadinessReport
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                unityVersion = Application.unityVersion,
                targetPlatform = Application.platform.ToString(),
                isXrActive = XRSettings.isDeviceActive,
                xrLoadedModel = XRSettings.loadedDeviceName,
                openXrRuntimeEnvironment = Environment.GetEnvironmentVariable("XR_RUNTIME_JSON") ?? "Not Set / Registry Default"
            };

            // 1. Read Simulator Configured Profile from Disk
            string simConfigPath = @"G:\Dev\MetaXRSimulator\runtime\PFiles\MetaXRSimulator\v207.0\config\sim_core_configuration.json";
            if (File.Exists(simConfigPath))
            {
                try
                {
                    string content = File.ReadAllText(simConfigPath);
                    if (content.Contains("\"device_profile\": \"Meta VR Glasses\""))
                    {
                        report.simulatorConfiguredProfile = "Meta VR Glasses";
                    }
                    else
                    {
                        report.simulatorConfiguredProfile = "Unknown / Other";
                    }
                }
                catch (Exception ex)
                {
                    report.simulatorConfiguredProfile = $"Error reading config: {ex.Message}";
                }
            }
            else
            {
                report.simulatorConfiguredProfile = "Simulator config not found";
            }

            // 2. Active Session Evidence
            if (XRSettings.isDeviceActive)
            {
                report.simulatorActiveSessionProfile = string.IsNullOrEmpty(XRSettings.loadedDeviceName)
                    ? "Active XR Device Connected (Querying OpenXR)"
                    : XRSettings.loadedDeviceName;
            }
            else
            {
                report.simulatorActiveSessionProfile = "OpenXR Session Not Active";
            }

            // 3. Inspect OVRProjectConfig
            InspectProjectConfig(report);

            // 4. Inspect Runtime Input Evidence from LookPinchTester
            InspectRuntimeInputEvidence(report);

            // 5. Query Input Devices
            var devices = new List<InputDevice>();
            InputDevices.GetDevices(devices);
            foreach (var dev in devices)
            {
                report.inputDevices.Add($"{dev.name} ({dev.characteristics})");
            }

            // 6. Meta Project Setup Tool Audit (if in Editor)
#if UNITY_EDITOR
            AuditMetaProjectSetup(report);
#endif

            CurrentReport = report;

            // Save JSON report
            try
            {
                string logsDir = Path.Combine(Application.dataPath, "..", "Logs");
                if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);
                string outPath = Path.Combine(logsDir, reportFileName);
                string json = JsonUtility.ToJson(report, true);
                File.WriteAllText(outPath, json);
                Debug.Log($"[DeviceReadinessCheck] Report saved to {outPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DeviceReadinessCheck] Failed to save report: {ex.Message}");
            }

            return report;
        }

        private void InspectProjectConfig(DeviceReadinessReport report)
        {
            try
            {
                var projectConfig = OVRProjectConfig.CachedProjectConfig;
                if (projectConfig != null)
                {
                    if (projectConfig.targetDeviceTypes != null && projectConfig.targetDeviceTypes.Count > 0)
                    {
                        report.configuredTargetDevices = string.Join(", ", projectConfig.targetDeviceTypes);
                    }
                    else
                    {
                        report.configuredTargetDevices = "None Specified";
                    }

                    report.configuredHandTrackingSupport = projectConfig.handTrackingSupport.ToString();
                    report.controllerFreeConfigured = (projectConfig.handTrackingSupport == OVRProjectConfig.HandTrackingSupport.HandsOnly);
                }
                else
                {
                    report.configuredTargetDevices = "OVRProjectConfig unavailable";
                    report.configuredHandTrackingSupport = "Unknown";
                    report.controllerFreeConfigured = false;
                }
            }
            catch (Exception ex)
            {
                report.notes.Add($"ProjectConfig inspection notice: {ex.Message}");
            }
        }

        private void InspectRuntimeInputEvidence(DeviceReadinessReport report)
        {
            var tester = FindFirstObjectByType<LookPinchTester>();
            if (tester != null)
            {
                report.gazeTargetEntered = (tester.gazeEnterCount > 0 || tester.IsGazed);
                report.gazeSource = tester.ActiveGazeSource;
                report.gazeEnterCount = tester.gazeEnterCount;

                report.pinchObserved = (tester.pinchStartCount > 0 || tester.IsPinched);
                report.pinchSource = tester.ActivePinchSource;
                report.pinchStartCount = tester.pinchStartCount;

                report.lookAndPinchTriggered = (tester.lookAndPinchTriggerCount > 0 || tester.LookAndPinchTriggered);
                report.lookAndPinchTriggerCount = tester.lookAndPinchTriggerCount;
            }
            else
            {
                report.gazeTargetEntered = false;
                report.gazeSource = "No LookPinchTester in scene";
                report.pinchObserved = false;
                report.pinchSource = "No LookPinchTester in scene";
                report.lookAndPinchTriggered = false;
            }
        }

#if UNITY_EDITOR
        private void AuditMetaProjectSetup(DeviceReadinessReport report)
        {
            try
            {
                var setupType = Type.GetType("OVRProjectSetup, Oculus.VR.Editor");
                if (setupType != null)
                {
                    var getTasksMethod = setupType.GetMethod("GetTasks", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(UnityEditor.BuildTargetGroup) }, null);
                    if (getTasksMethod != null)
                    {
                        var tasks = getTasksMethod.Invoke(null, new object[] { UnityEditor.BuildTargetGroup.Android }) as IEnumerable;
                        if (tasks != null)
                        {
                            foreach (var task in tasks)
                            {
                                var levelProp = task.GetType().GetProperty("Level");
                                var messageProp = task.GetType().GetProperty("Message");
                                var isDoneProp = task.GetType().GetProperty("IsDone");
                                var validProp = task.GetType().GetProperty("Valid");

                                object validObj = validProp?.GetValue(task);
                                if (validObj != null)
                                {
                                    var getValidMethod = validObj.GetType().GetMethod("GetValue", new[] { typeof(UnityEditor.BuildTargetGroup) });
                                    bool isValid = (bool)(getValidMethod?.Invoke(validObj, new object[] { UnityEditor.BuildTargetGroup.Android }) ?? true);
                                    if (!isValid) continue;
                                }

                                bool isDone = false;
                                if (isDoneProp != null)
                                {
                                    var isDoneDelegate = isDoneProp.GetValue(task) as Delegate;
                                    if (isDoneDelegate != null)
                                    {
                                        try
                                        {
                                            isDone = (bool)isDoneDelegate.DynamicInvoke(UnityEditor.BuildTargetGroup.Android);
                                        }
                                        catch
                                        {
                                            isDone = false;
                                        }
                                    }
                                }

                                if (!isDone)
                                {
                                    object levelObj = levelProp?.GetValue(task);
                                    object msgObj = messageProp?.GetValue(task);

                                    string levelStr = "";
                                    if (levelObj != null)
                                    {
                                        var getValMethod = levelObj.GetType().GetMethod("GetValue", new[] { typeof(UnityEditor.BuildTargetGroup) });
                                        levelStr = getValMethod?.Invoke(levelObj, new object[] { UnityEditor.BuildTargetGroup.Android })?.ToString() ?? levelObj.ToString();
                                    }

                                    string msgStr = "";
                                    if (msgObj != null)
                                    {
                                        var getValMethod = msgObj.GetType().GetMethod("GetValue", new[] { typeof(UnityEditor.BuildTargetGroup) });
                                        msgStr = getValMethod?.Invoke(msgObj, new object[] { UnityEditor.BuildTargetGroup.Android })?.ToString() ?? msgObj.ToString();
                                    }

                                    if (levelStr.Equals("Required", StringComparison.OrdinalIgnoreCase))
                                    {
                                        report.criticalCount++;
                                        report.criticalMessages.Add(msgStr);
                                    }
                                    else if (levelStr.Equals("Recommended", StringComparison.OrdinalIgnoreCase))
                                    {
                                        report.recommendationCount++;
                                        report.recommendationMessages.Add(msgStr);
                                    }
                                    else
                                    {
                                        report.warningCount++;
                                        report.warningMessages.Add(msgStr);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                report.notes.Add($"Meta Project Setup audit notice: {ex.Message}");
            }
        }
#endif
    }
}
