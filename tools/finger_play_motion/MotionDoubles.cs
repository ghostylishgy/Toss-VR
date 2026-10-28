// OFFLINE TEST DOUBLES ONLY. No Unity/Oculus assemblies, no native calls, no engine runtime.
// These are deterministic managed models used to execute production methods verbatim off-engine.
// They do NOT validate Unity physics, the XR player loop, real SDK event ordering or XR tracking.
using System;
using System.Collections.Generic;
using System.Linq;
using TossMotionAudit;

namespace TossMotionAudit
{
    public static class Audit
    {
        public static readonly List<string> Events = new List<string>();
        public static int KinematicVelocityWrites, WarningLogs;
        public static Action<string> OnLog;
        public static void Add(string text) { Events.Add(text); }
        public static void Reset()
        {
            Events.Clear(); KinematicVelocityWrites = WarningLogs = 0;
            OnLog = null;
            UnityEngine.Application.isPlaying = true;
            UnityEngine.Time.frameCount = 0;
            UnityEngine.Time.realtimeSinceStartupAsDouble = 0;
            UnityEngine.Time.deltaTime = 0.02f; UnityEngine.Time.fixedDeltaTime = 0.02f;
            UnityEngine.Physics.gravity = new UnityEngine.Vector3(0, -9.81f, 0);
        }
        public static bool Any(Func<string, bool> predicate) { return Events.Any(predicate); }
    }
}

namespace UnityEngine
{
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class SerializeField : Attribute { }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class DisallowMultipleComponent : Attribute { }
    public enum RigidbodyInterpolation { None, Interpolate }
    public class Object
    {
        private static int nextId;
        private readonly int id = ++nextId;
        public int GetInstanceID() => id;
    }
    public class GameObject : Object
    {
        public string name = "test-object";
        public bool activeInHierarchy = true;
        public object[] Components = Array.Empty<object>();
        public bool TryGetComponent<T>(out T component)
        {
            component = Components.OfType<T>().FirstOrDefault();
            return component != null;
        }
    }
    public class Component : Object
    {
        public GameObject gameObject = new GameObject();
        public Transform transform = new Transform();
        public object[] Children = Array.Empty<object>();
        public Component() { gameObject.Components = new object[] { this }; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) => Children.OfType<T>().ToArray();
    }
    public class Behaviour : Component
    {
        public bool enabled = true;
        public bool isActiveAndEnabled { get => enabled && gameObject.activeInHierarchy; set => enabled = value; }
    }
    public class MonoBehaviour : Behaviour { }

    // Float component arithmetic mirrors Unity's managed Vector3 surface used by production code.
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public float sqrMagnitude => (float)((double)x * x + (double)y * y + (double)z * z);
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public Vector3 normalized
        {
            get
            {
                float m = magnitude;
                return m > 1e-8f ? this / m : zero;
            }
        }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float f) => new Vector3(a.x * f, a.y * f, a.z * f);
        public static Vector3 operator /(Vector3 a, float f) => new Vector3(a.x / f, a.y / f, a.z / f);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static float Dot(Vector3 a, Vector3 b)
            => (float)((double)a.x * b.x + (double)a.y * b.y + (double)a.z * b.z);
        public static Vector3 ClampMagnitude(Vector3 v, float max) => v.sqrMagnitude > max * max ? v.normalized * max : v;
        public override string ToString() => string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "({0:R},{1:R},{2:R})", x, y, z);
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);

        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
            (float)((double)a.w * b.x + (double)a.x * b.w + (double)a.y * b.z - (double)a.z * b.y),
            (float)((double)a.w * b.y - (double)a.x * b.z + (double)a.y * b.w + (double)a.z * b.x),
            (float)((double)a.w * b.z + (double)a.x * b.y - (double)a.y * b.x + (double)a.z * b.w),
            (float)((double)a.w * b.w - (double)a.x * b.x - (double)a.y * b.y - (double)a.z * b.z));

        public static Quaternion Inverse(Quaternion q)
        {
            float n = (float)((double)q.x * q.x + (double)q.y * q.y + (double)q.z * q.z + (double)q.w * q.w);
            return new Quaternion(-q.x / n, -q.y / n, -q.z / n, q.w / n);
        }

        // Models Unity's Quaternion.ToAngleAxis: angle is 2*acos(clamped w) so it can exceed 180 degrees
        // when w is negative, which is exactly why production subtracts 360.
        public void ToAngleAxis(out float angleDegrees, out Vector3 axis)
        {
            double cw = Math.Max(-1.0, Math.Min(1.0, w));
            if (double.IsNaN(cw))
            {
                angleDegrees = float.NaN;
                axis = new Vector3(x, y, z);
                return;
            }
            double angleRad = 2 * Math.Acos(cw);
            angleDegrees = (float)(angleRad * 180 / Math.PI);
            double sinHalf = Math.Sin(angleRad * 0.5);
            axis = sinHalf > 1e-6f
                ? new Vector3((float)(x / sinHalf), (float)(y / sinHalf), (float)(z / sinHalf)).normalized
                : new Vector3(1, 0, 0);
        }

        // Fixture-only construction helper (Unity API surface, not production logic).
        public static Quaternion AxisAngle(Vector3 axis, float radians)
        {
            Vector3 a = axis.normalized;
            double s = Math.Sin(radians * 0.5);
            return new Quaternion((float)(a.x * s), (float)(a.y * s), (float)(a.z * s), (float)Math.Cos(radians * 0.5));
        }

        public float sqrMagnitude => (float)((double)x * x + (double)y * y + (double)z * z + (double)w * w);
    }

    public struct Pose
    {
        public Vector3 position; public Quaternion rotation;
        public Pose(Vector3 p, Quaternion q) { position = p; rotation = q; }
    }

    public class Transform
    {
        public Vector3 position; public Quaternion rotation = Quaternion.identity;
        public Pose GetPose() => new Pose(position, rotation);
        public void SetPositionAndRotation(Vector3 p, Quaternion q) { position = p; rotation = q; }
    }

    public static class Application { public static bool isPlaying = true; }
    public static class Time
    {
        public static float deltaTime = 0.02f, fixedDeltaTime = 0.02f;
        public static int frameCount;
        public static double realtimeSinceStartupAsDouble;
    }
    public static class Physics { public static Vector3 gravity = new Vector3(0, -9.81f, 0); }
    public static class Mathf
    {
        public const float Deg2Rad = (float)Math.PI / 180;
        public const float Rad2Deg = 180 / (float)Math.PI;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Abs(float a) => a < 0f ? -a : a;
        public static float Sqrt(float a) => (float)Math.Sqrt(a);
        public static float Clamp(float value, float min, float max) => value < min ? min : (value > max ? max : value);
    }

    public static class Debug
    {
        public static void Log(string s) { Audit.Add("log:" + s); Audit.OnLog?.Invoke(s); }
        public static void LogWarning(string s) { Audit.WarningLogs++; Audit.Add("warning:" + s); }
    }

    // Records the write order production uses, and reproduces Unity's kinematic-velocity warning shape.
    public class Rigidbody
    {
        private bool kinematic = true;
        private Vector3 linear, angular;
        public bool useGravity = false;
        public float linearDamping, angularDamping;
        public float maxAngularVelocity = 7f;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        // Synthetic external physics corruption: does not pretend production wrote the invalid value.
        public void SupplyVelocity(Vector3 l, Vector3 a) { linear = l; angular = a; }
        public RigidbodyInterpolation interpolation;
        public bool isKinematic
        {
            get => kinematic;
            set { Audit.Add("kinematic:" + value); if (kinematic && !value) Audit.Add("dynamic-transition"); kinematic = value; }
        }
        private void Write(string kind, Vector3 value)
        {
            Audit.Add(kind + ":" + value);
            if (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z)
                || float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z))
                Audit.Add("NON-FINITE-VELOCITY-WRITE");
            if (kinematic) { Audit.KinematicVelocityWrites++; Audit.Add("KINEMATIC-VELOCITY-WARNING"); }
        }
        public Vector3 linearVelocity { get => linear; set { Write("linear", value); linear = value; } }
        public Vector3 angularVelocity { get => angular; set { Write("angular", value); angular = value; } }
    }
}

namespace Oculus.Interaction
{
    public enum PointerEventType { Select, Move, Unselect, Cancel }

    public struct PointerEvent
    {
        public int Identifier; public PointerEventType Type; public UnityEngine.Pose Pose;
        public PointerEvent(int id, PointerEventType type, UnityEngine.Pose pose) { Identifier = id; Type = type; Pose = pose; }
    }

    // Shape-only stand-in: exposes the real SDK collection type (List<Pose> SelectingPoints) that
    // production reads. No SDK behaviour is executed, so nothing here validates real selection semantics.
    public class Grabbable : IPointableElement
    {
        public List<UnityEngine.Pose> SelectingPoints { get; set; } = new List<UnityEngine.Pose>();
        public int SelectingPointsCount => SelectingPoints == null ? 0 : SelectingPoints.Count;
        public List<UnityEngine.Pose> GrabPoints { get; set; } = new List<UnityEngine.Pose>();
        public event Action<PointerEvent> WhenPointerEventRaised;
        public int SubscriberCount => WhenPointerEventRaised?.GetInvocationList().Length ?? 0;
        public int ProcessedEvents;
        public bool SyntheticCancelRemovesSelection = true;
        public void ProcessPointerEvent(PointerEvent evt)
        {
            ProcessedEvents++;
            // This supplied cancellation policy is not the SDK event pipeline.
            if (evt.Type == PointerEventType.Cancel && SyntheticCancelRemovesSelection) SelectingPoints?.Clear();
            WhenPointerEventRaised?.Invoke(evt);
        }
    }
    public interface IPointableElement { }
    public interface IGameObjectFilter { bool Filter(UnityEngine.GameObject interactorGo); }
    public enum InteractorState { Normal, Hover, Select, Disabled }
    public interface IInteractorView { int Identifier { get; } InteractorState State { get; } }
    public interface IInteractableView { IEnumerable<IInteractorView> SelectingInteractorViews { get; } }
    public class TestInteractor : UnityEngine.MonoBehaviour, IInteractorView
    {
        public int Identifier { get; set; }
        public InteractorState State { get; set; } = InteractorState.Normal;
    }
    public class TestInteractable : UnityEngine.MonoBehaviour, IInteractableView
    {
        public IPointableElement PointableElement { get; set; }
        public IEnumerable<IInteractorView> SelectingInteractorViews { get; set; } = Array.Empty<IInteractorView>();
    }
    public class GazeInteractor : TestInteractor { }
    public class GazeInteractable : TestInteractable { }
}

namespace Oculus.Interaction.Input
{
    // Minimal API shapes only. All raw flags/poses/versions are synthetic inputs, never XR evidence.
    [Flags]
    public enum HandFingerFlags { None = 0, Thumb = 1, Index = 2, Middle = 4, Ring = 8, Pinky = 16, All = 31 }
    public enum PoseOrigin { None, RawTrackedPose, FilteredTrackedPose, SyntheticPose }
    public class HandDataAsset
    {
        public bool IsDataValid, IsConnected, IsTracked, IsHighConfidence;
        public UnityEngine.Pose Root;
        public PoseOrigin RootPoseOrigin;
        public bool[] IsFingerPinching = new bool[5];
        public bool[] IsFingerHighConfidence = new bool[5];
    }
    public interface IDataSource<T> { T GetData(); int CurrentDataVersion { get; } }
    public class DataModifier<T> : UnityEngine.MonoBehaviour, IDataSource<T>
    {
        public IDataSource<T> ModifyDataFromSource { get; set; }
        public T GetData() => ModifyDataFromSource.GetData();
        public int CurrentDataVersion => ModifyDataFromSource.CurrentDataVersion;
    }
    // The real raw source is a data source, NOT IHand. FindRawTossSource must unwrap the chain.
    public class FromOVRHandDataSource : UnityEngine.MonoBehaviour, IDataSource<HandDataAsset>
    {
        public HandDataAsset Data = new HandDataAsset();
        public int CurrentDataVersion { get; set; }
        public int ReadCount;
        public HandDataAsset GetData() { ReadCount++; return Data; }
    }
    public interface IHand
    {
        bool IsConnected { get; }
        bool IsHighConfidence { get; }
        bool IsTrackedDataValid { get; }
        int CurrentDataVersion { get; }
    }
    public class Hand : DataModifier<HandDataAsset>, IHand
    {
        // Models a last-good/fallback hand whose public flags can remain valid over invalid raw data.
        public bool IsConnected { get; set; } = true;
        public bool IsHighConfidence { get; set; } = true;
        public bool IsTrackedDataValid { get; set; } = true;
    }
    public class HandRef : UnityEngine.MonoBehaviour, IHand
    {
        public IHand Hand { get; set; }
        public bool IsConnected => Hand.IsConnected;
        public bool IsHighConfidence => Hand.IsHighConfidence;
        public bool IsTrackedDataValid => Hand.IsTrackedDataValid;
        public int CurrentDataVersion => Hand.CurrentDataVersion;
    }
    public class UnreadableHand : UnityEngine.MonoBehaviour, IHand
    {
        public bool IsConnected => throw new InvalidOperationException("Diagnostic must not update input data");
        public bool IsHighConfidence => throw new InvalidOperationException("Diagnostic must not update input data");
        public bool IsTrackedDataValid => throw new InvalidOperationException("Diagnostic must not update input data");
        public int CurrentDataVersion => throw new InvalidOperationException("Diagnostic must not update input data");
    }
}

namespace Oculus.Interaction.Grab
{
    [Flags]
    public enum GrabTypeFlags { None = 0, Pinch = 1, Palm = 2, All = 3 }
}

namespace Oculus.Interaction.GrabAPI
{
    using Oculus.Interaction.Input;
    public struct GrabbingRule { }
    public class HandGrabAPI : UnityEngine.MonoBehaviour
    {
        public IHand Hand { get; set; }
        public bool SyntheticPinch, SyntheticPalm, SyntheticUnselectEdge;
        public HandFingerFlags SyntheticSustainMask = HandFingerFlags.Thumb | HandFingerFlags.Index;
        public HandFingerFlags LastSustainingFingers;
        // This synthetic rule uses the passed raw mask, independently of the supplied SDK pinch bool.
        public bool IsSustainingGrab(in GrabbingRule rule, HandFingerFlags fingers)
        {
            LastSustainingFingers = fingers;
            return (fingers & SyntheticSustainMask) != 0;
        }
        public bool IsHandPinchGrabbing(in GrabbingRule rule) => SyntheticPinch;
        public bool IsHandPalmGrabbing(in GrabbingRule rule) => SyntheticPalm;
        public bool IsHandUnselectPinchFingersChanged(in GrabbingRule rule) => SyntheticUnselectEdge;
    }
}

namespace Oculus.Interaction.HandGrab
{
    using Oculus.Interaction.Grab;
    using Oculus.Interaction.GrabAPI;
    public interface IHandGrabInteractable
    {
        GrabTypeFlags SupportedGrabTypes { get; }
        GrabbingRule PinchGrabRules { get; }
        GrabbingRule PalmGrabRules { get; }
    }
    public class HandGrabInteractor : Oculus.Interaction.TestInteractor
    {
        public Oculus.Interaction.Input.IHand Hand { get; set; }
        public HandGrabAPI HandGrabApi { get; set; }
        // Preserve the SDK distinction: TargetInteractable is an interface, SelectedInteractable concrete.
        public IHandGrabInteractable TargetInteractable { get; set; }
        public HandGrabInteractable SelectedInteractable { get; set; }
    }
    public class DistanceHandGrabInteractor : Oculus.Interaction.TestInteractor
    {
        public Oculus.Interaction.Input.IHand Hand { get; set; }
        public HandGrabAPI HandGrabApi { get; set; }
    }
    public class HandGrabInteractable : Oculus.Interaction.TestInteractable, IHandGrabInteractable
    {
        public GrabTypeFlags SupportedGrabTypes { get; set; } = GrabTypeFlags.All;
        public GrabbingRule PinchGrabRules { get; set; }
        public GrabbingRule PalmGrabRules { get; set; }
    }
    public class DistanceHandGrabInteractable : Oculus.Interaction.TestInteractable { }
}

namespace Toss.Coin
{
    public class CoinPresenter
    {
        private bool value = true;
        public bool enabled { get => value; set { this.value = value; Audit.Add("presenter:" + value); } }
    }

    public partial class FingerPlayHarness
    {
        // Discovery seam only. Real code queries OVRHand through the engine scene; offline tests never do.
        private string ResolveActiveHand(UnityEngine.Vector3 position, out UnityEngine.GameObject handGo)
        {
            handGo = new UnityEngine.GameObject();
            return "TEST-DOUBLE-HAND";
        }
    }
}
