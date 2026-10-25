// P1.3 A-wrap pre-integration feasibility gate. OFFLINE ONLY.
// Thin deterministic doubles reproduce the call ORDER of Meta ISDK v207 Grabbable.ProcessPointerEvent /
// PointableElement / GrabFreeTransformer Begin-Update-End (see Grabbable.cs:249-379, PointableElement.cs:209-330,
// GrabFreeTransformer.cs:150-203). They are not the SDK, not Unity and not XR input.
// CoinFollowCoreProto is an experiment-local prototype of the wrapper contract; it is not production code.
using System;
using System.Collections.Generic;
using System.Numerics;
using Toss.Experiments.PerceivedWeight;

namespace Toss.Experiments.AwrapGate
{
    public enum PointerEventType { Hover, Unhover, Select, Unselect, Move, Cancel }

    public readonly struct PointerEvent
    {
        public int Identifier { get; }
        public PointerEventType Type { get; }
        public FollowPose Pose { get; }
        public PointerEvent(int id, PointerEventType type, FollowPose pose) { Identifier = id; Type = type; Pose = pose; }
    }

    public sealed class TransformDouble
    {
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.Identity;
        public readonly List<FollowPose> Writes = new List<FollowPose>();
        public FollowPose Pose => new FollowPose(Position, Rotation);
        public void Write(FollowPose p) { Position = p.Position; Rotation = p.Rotation; Writes.Add(p); }
    }

    public interface ITransformer
    {
        void Initialize(GrabbableDouble grabbable);
        void BeginTransform();
        void UpdateTransform();
        void EndTransform();
    }

    // Contract double of GrabFreeTransformer: one grab point, pose follows the grab point with the delta captured at Begin.
    // EndTransform with no rented deltas throws like ArrayPool<T>.Shared.Return(null) (GrabFreeTransformer.cs:201).
    public sealed class GrabFreeTransformerDouble : ITransformer
    {
        private GrabbableDouble _grabbable;
        private object _deltas;
        private Vector3 _grabDeltaLocal;
        private Quaternion _rotationDelta;
        public int InitializeCount, BeginCount, UpdateCount, EndCount, LeakedRents;

        public void Initialize(GrabbableDouble grabbable) { InitializeCount++; _grabbable = grabbable; }

        public void BeginTransform()
        {
            if (_grabbable == null) throw new NullReferenceException("BeginTransform before Initialize");
            if (_deltas != null) LeakedRents++;
            BeginCount++;
            _deltas = new object();
            var gp = _grabbable.GrabPoints[0];
            var t = _grabbable.Transform.Pose;
            _rotationDelta = Quaternion.Conjugate(gp.Rotation) * t.Rotation;
            _grabDeltaLocal = Vector3.Transform(t.Position - gp.Position, Quaternion.Conjugate(gp.Rotation));
        }

        public void UpdateTransform()
        {
            if (_deltas == null) throw new NullReferenceException("UpdateTransform without BeginTransform");
            UpdateCount++;
            var gp = _grabbable.GrabPoints[0];
            _grabbable.Transform.Write(new FollowPose(gp.Position + Vector3.Transform(_grabDeltaLocal, gp.Rotation),
                gp.Rotation * _rotationDelta));
        }

        public void EndTransform()
        {
            if (_deltas == null) throw new ArgumentNullException("array");
            EndCount++;
            _deltas = null;
        }
    }

    public sealed class GrabbableDouble
    {
        public readonly TransformDouble Transform = new TransformDouble();
        public readonly List<FollowPose> GrabPoints = new List<FollowPose>();
        private readonly List<int> _selectingIds = new List<int>();
        private readonly List<int> _pointIds = new List<int>();
        private readonly List<FollowPose> _points = new List<FollowPose>();
        private ITransformer _oneGrabTransformer, _activeTransformer;
        private bool _started;
        public int MaxGrabPoints = 1;
        public event Action<PointerEvent> WhenPointerEventRaised;
        public bool HasActiveTransformer => _activeTransformer != null;

        public void InjectOptionalOneGrabTransformer(ITransformer t) { _oneGrabTransformer = t; }

        // Grabbable.Start (194-198): Initialize exactly once on the referenced transformer.
        public void Start()
        {
            if (_oneGrabTransformer != null) _oneGrabTransformer.Initialize(this);
            _started = true;
        }

        // Grabbable.ProcessPointerEvent (249-279), order copied verbatim.
        public void ProcessPointerEvent(PointerEvent evt)
        {
            switch (evt.Type)
            {
                case PointerEventType.Select: EndTransform(); break;
                case PointerEventType.Unselect: ForceMove(evt); EndTransform(); break;
                case PointerEventType.Cancel: EndTransform(); break;
            }

            BaseProcessPointerEvent(evt);

            switch (evt.Type)
            {
                case PointerEventType.Select: BeginTransform(); break;
                case PointerEventType.Unselect: BeginTransform(); break;
                case PointerEventType.Move: UpdateTransform(); break;
            }
        }

        // Grabbable.OnDisable (222-229) then PointableElement.OnDisable (181-195).
        public void OnDisable()
        {
            if (_started) EndTransform();
            if (_started)
            {
                while (GrabPoints.Count > 0)
                    Cancel(new PointerEvent(_selectingIds[0], PointerEventType.Cancel, GrabPoints[0]));
            }
        }

        // PointableElement.ProcessPointerEvent (209-232) and the private handlers (234-330).
        private void BaseProcessPointerEvent(PointerEvent evt)
        {
            switch (evt.Type)
            {
                case PointerEventType.Hover:
                    _pointIds.Add(evt.Identifier); _points.Add(evt.Pose); Raise(evt); break;
                case PointerEventType.Unhover:
                {
                    int i = _pointIds.IndexOf(evt.Identifier);
                    if (i == -1) return;
                    _pointIds.RemoveAt(i); _points.RemoveAt(i); Raise(evt); break;
                }
                case PointerEventType.Move:
                {
                    int i = _pointIds.IndexOf(evt.Identifier);
                    if (i == -1) return;
                    _points[i] = evt.Pose;
                    int s = _selectingIds.IndexOf(evt.Identifier);
                    if (s != -1) GrabPoints[s] = evt.Pose;
                    Raise(evt); break;
                }
                case PointerEventType.Select:
                    _selectingIds.Add(evt.Identifier); GrabPoints.Add(evt.Pose); Raise(evt); break;
                case PointerEventType.Unselect:
                {
                    int s = _selectingIds.IndexOf(evt.Identifier);
                    if (s == -1) return;
                    _selectingIds.RemoveAt(s); GrabPoints.RemoveAt(s); Raise(evt); break;
                }
                case PointerEventType.Cancel: Cancel(evt); break;
            }
        }

        private void Cancel(PointerEvent evt)
        {
            int s = _selectingIds.IndexOf(evt.Identifier);
            if (s != -1) { _selectingIds.RemoveAt(s); GrabPoints.RemoveAt(s); }
            int i = _pointIds.IndexOf(evt.Identifier);
            if (i != -1) { _pointIds.RemoveAt(i); _points.RemoveAt(i); }
            Raise(evt);
        }

        private void Raise(PointerEvent evt) { WhenPointerEventRaised?.Invoke(evt); }

        // Grabbable.ForceMove (314-319)
        private void ForceMove(PointerEvent releaseEvent)
        {
            ProcessPointerEvent(new PointerEvent(releaseEvent.Identifier, PointerEventType.Move, releaseEvent.Pose));
        }

        // Grabbable.BeginTransform (323-352)
        private void BeginTransform()
        {
            EndTransform();
            int useGrabPoints = GrabPoints.Count;
            if (MaxGrabPoints != -1) useGrabPoints = Math.Min(useGrabPoints, MaxGrabPoints);
            _activeTransformer = useGrabPoints == 1 ? _oneGrabTransformer : null;
            if (_activeTransformer == null) return;
            _activeTransformer.BeginTransform();
        }

        // Grabbable.UpdateTransform (354-362)
        private void UpdateTransform()
        {
            if (_activeTransformer == null) return;
            _activeTransformer.UpdateTransform();
        }

        // Grabbable.EndTransform (364-372)
        private void EndTransform()
        {
            if (_activeTransformer == null) return;
            _activeTransformer.EndTransform();
            _activeTransformer = null;
        }
    }

    // Prototype of the A-wrap contract. Rigid = pure pass-through. SpringDamper = inner write, then one follower write.
    // Same-frame repeats (dt <= 0) re-anchor the carried lag error onto the new target instead of calling Step(dt=0).
    public sealed class CoinFollowCoreProto : ITransformer
    {
        private readonly ITransformer _inner;
        private readonly TransformDouble _transform;
        private readonly Func<double> _clock;
        private readonly PoseFollower _follower;
        private readonly FollowMode _mode;
        private readonly bool _naiveSameFrame;
        private bool _began;
        private double _lastTime;
        private FollowPose _lastTarget, _lastOutput;
        public int BeginWhileBegun, EndWithoutBegin, UpdateWithoutBegin, SameFrameCarries, TimeGapResets, PassThroughFallbacks;
        public FollowFlags LastFlags;
        public bool Began => _began;
        public Vector3 LinearVelocity => _follower.LinearVelocity;
        public Vector3 AngularVelocity => _follower.AngularVelocity;
        public FollowPose FollowerOutput => _follower.OutputPose;

        public CoinFollowCoreProto(ITransformer inner, TransformDouble transform, Func<double> clock, FollowMode mode,
            bool naiveSameFrame = false)
        {
            _inner = inner; _transform = transform; _clock = clock; _mode = mode; _naiveSameFrame = naiveSameFrame;
            _follower = new PoseFollower(new FollowParameters(), new ExperimentCondition(mode, SoundCondition.Off),
                transform.Pose);
        }

        public void Initialize(GrabbableDouble grabbable) { _inner.Initialize(grabbable); }

        public void BeginTransform()
        {
            if (_began) { BeginWhileBegun++; EndTransform(); }
            _inner.BeginTransform();
            _began = true;
            var pose = _transform.Pose;
            _follower.Reset(pose);
            _lastTarget = _lastOutput = pose;
            _lastTime = _clock();
        }

        public void UpdateTransform()
        {
            if (!_began) { UpdateWithoutBegin++; return; }
            _inner.UpdateTransform();
            if (_mode == FollowMode.Rigid) return;

            var target = _transform.Pose;
            if (!_naiveSameFrame && !IsFinite(target)) { PassThroughFallbacks++; return; }
            double now = _clock();
            double dt = now - _lastTime;
            FollowPose output;
            if (dt <= 0 && !_naiveSameFrame)
            {
                SameFrameCarries++;
                var carriedRotation = _lastOutput.Rotation * Quaternion.Conjugate(_lastTarget.Rotation);
                output = new FollowPose(target.Position + (_lastOutput.Position - _lastTarget.Position),
                    Quaternion.Normalize(carriedRotation * target.Rotation));
            }
            else
            {
                var step = _follower.Step(_lastOutput, target, Math.Max(dt, 0));
                output = step.Pose;
                LastFlags = step.Flags;
                if ((step.Flags & FollowFlags.TimeGapReset) != 0) TimeGapResets++;
                _lastTime = now;
            }
            _lastTarget = target;
            _lastOutput = output;
            _transform.Write(output);
        }

        public void EndTransform()
        {
            if (!_began) { EndWithoutBegin++; return; }
            _began = false;
            _inner.EndTransform();
            var pose = _transform.Pose;
            if (IsFinite(pose)) _follower.Reset(pose);
        }

        private static bool IsFinite(FollowPose p)
        {
            return IsFinite(p.Position.X) && IsFinite(p.Position.Y) && IsFinite(p.Position.Z)
                && IsFinite(p.Rotation.X) && IsFinite(p.Rotation.Y) && IsFinite(p.Rotation.Z) && IsFinite(p.Rotation.W)
                && Math.Abs(p.Position.X) <= 1000 && Math.Abs(p.Position.Y) <= 1000 && Math.Abs(p.Position.Z) <= 1000
                && p.Rotation.LengthSquared() > 1e-12f;
        }

        private static bool IsFinite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    }
}
