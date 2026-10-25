// P2 FLIGHT DYNAMICS ISOLATED | NOT INTEGRATED | NOT RUNTIME VALIDATED | NO RNG / NO RESULT / NO CATCH
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Toss.Experiments.Flight
{
    public readonly struct IntentIdentity : IEquatable<IntentIdentity>
    {
        public long Session { get; }
        public long Sequence { get; }
        public bool IsValid => Session > 0 && Sequence > 0;
        public IntentIdentity(long session, long sequence) { Session = session; Sequence = sequence; }
        public bool Equals(IntentIdentity other) => Session == other.Session && Sequence == other.Sequence;
        public override bool Equals(object obj) => obj is IntentIdentity other && Equals(other);
        public override int GetHashCode() => unchecked(Session.GetHashCode() * 397 ^ Sequence.GetHashCode());
    }

    public enum FlightStatus { Idle, Running, Stopped }
    public enum FlightPhase { None, Rising, Apex, Falling, Coasting }
    public enum StopReason { None, SpatialBoundary, SpeedLimit, TimeLimit, TimeGap, NumericalFailure }
    public enum RequestReason { Accepted, InvalidIdentity, DuplicateIntent, Busy, InvalidLaunch, NotRunning, InvalidDeltaTime, TimeGap }

    public sealed class FlightLimits
    {
        public Vector3 Min { get; }
        public Vector3 Max { get; }
        public double MaxDuration { get; }
        public double MaxDeltaTime { get; }
        public double MaxLinearSpeed { get; }
        public double MaxAngularSpeed { get; }
        public double MaxGravity { get; }

        public FlightLimits(Vector3 min, Vector3 max, double maxDuration = 1.5, double maxDeltaTime = 0.1,
            double maxLinearSpeed = 6, double maxAngularSpeed = 30, double maxGravity = 20)
        {
            if (!Numbers.InDomain(min) || !Numbers.InDomain(max) || min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z)
                throw new ArgumentOutOfRangeException(nameof(min));
            foreach (double value in new[] { maxDuration, maxDeltaTime, maxLinearSpeed, maxAngularSpeed, maxGravity })
                if (!Numbers.Finite(value) || value < Numbers.MinLimit || value > Numbers.DomainLimit)
                    throw new ArgumentOutOfRangeException(nameof(value), "Supported scalar limit domain is [1e-6, 1e6].");
            Min = min; Max = max; MaxDuration = maxDuration; MaxDeltaTime = maxDeltaTime;
            MaxLinearSpeed = maxLinearSpeed; MaxAngularSpeed = maxAngularSpeed; MaxGravity = maxGravity;
        }

        public static FlightLimits Experimental() => new FlightLimits(new Vector3(-0.75f, 0.25f, 0.15f), new Vector3(0.75f, 2.5f, 1.5f));
        public bool Contains(Vector3 p) => Numbers.Finite(p) && p.X >= Min.X && p.X <= Max.X
            && p.Y >= Min.Y && p.Y <= Max.Y && p.Z >= Min.Z && p.Z <= Max.Z;
    }

    public readonly struct LaunchInput
    {
        public IntentIdentity Intent { get; }
        public Vector3 Position { get; }
        public Quaternion Orientation { get; }
        public Vector3 LinearVelocity { get; }
        public Vector3 AngularVelocity { get; }
        public Vector3 Gravity { get; }

        public LaunchInput(IntentIdentity intent, Vector3 position, Quaternion orientation, Vector3 linearVelocity,
            Vector3 angularVelocity, Vector3 gravity)
        {
            Intent = intent; Position = position; Orientation = orientation; LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity; Gravity = gravity;
        }
    }

    public readonly struct FlightState
    {
        public Vector3 Position { get; }
        public Quaternion Orientation { get; }
        public Vector3 LinearVelocity { get; }
        public Vector3 AngularVelocity { get; }
        public double Elapsed { get; }
        public FlightStatus Status { get; }
        public FlightPhase Phase { get; }
        public StopReason StopReason { get; }
        public IntentIdentity Intent { get; }
        public bool RequestAccepted { get; }
        public RequestReason RequestReason { get; }

        public FlightState(Vector3 position, Quaternion orientation, Vector3 linear, Vector3 angular, double elapsed,
            FlightStatus status, FlightPhase phase, StopReason stop, IntentIdentity intent, bool accepted, RequestReason reason)
        {
            Position = position; Orientation = orientation; LinearVelocity = linear; AngularVelocity = angular; Elapsed = elapsed;
            Status = status; Phase = phase; StopReason = stop; Intent = intent; RequestAccepted = accepted; RequestReason = reason;
        }
    }

    public sealed class FlightDynamics
    {
        public FlightLimits Limits { get; }
        public FlightState Current { get; private set; }
        private readonly HashSet<IntentIdentity> _consumed = new HashSet<IntentIdentity>();
        private LaunchInput _launch;
        private Quaternion _initialOrientation;
        private double _stopTime;
        private StopReason _plannedStop;

        public FlightDynamics(FlightLimits limits)
        {
            Limits = limits ?? throw new ArgumentNullException(nameof(limits));
            Reset();
        }

        public FlightState Reset()
        {
            _launch = default;
            _initialOrientation = Quaternion.Identity;
            _stopTime = 0;
            _plannedStop = StopReason.None;
            // Reset clears motion, not the lifetime replay barrier for already consumed intents.
            Current = new FlightState(Vector3.Zero, Quaternion.Identity, Vector3.Zero, Vector3.Zero, 0,
                FlightStatus.Idle, FlightPhase.None, StopReason.None, default, true, RequestReason.Accepted);
            return Current;
        }

        public FlightState Launch(LaunchInput input)
        {
            if (!input.Intent.IsValid) return Reject(RequestReason.InvalidIdentity);
            if (_consumed.Contains(input.Intent)) return Reject(RequestReason.DuplicateIntent);
            if (Current.Status != FlightStatus.Idle) return Reject(RequestReason.Busy);
            if (!Limits.Contains(input.Position) || !Numbers.Finite(input.LinearVelocity) || !Numbers.Finite(input.AngularVelocity)
                || !Numbers.Finite(input.Gravity) || Numbers.Length(input.LinearVelocity) > Limits.MaxLinearSpeed
                || Numbers.Length(input.AngularVelocity) > Limits.MaxAngularSpeed || Numbers.Length(input.Gravity) > Limits.MaxGravity
                || !Numbers.TryNormalize(input.Orientation, out var orientation)) return Reject(RequestReason.InvalidLaunch);

            _launch = input;
            _initialOrientation = orientation;
            _stopTime = Limits.MaxDuration;
            _plannedStop = StopReason.TimeLimit;
            for (int axis = 0; axis < 3; axis++)
            {
                PlanBoundary(Numbers.Axis(input.Position, axis), Numbers.Axis(input.LinearVelocity, axis), Numbers.Axis(input.Gravity, axis),
                    Numbers.Axis(Limits.Min, axis), false);
                PlanBoundary(Numbers.Axis(input.Position, axis), Numbers.Axis(input.LinearVelocity, axis), Numbers.Axis(input.Gravity, axis),
                    Numbers.Axis(Limits.Max, axis), true);
            }
            double a = Numbers.Dot(input.Gravity, input.Gravity);
            double b = 2 * Numbers.Dot(input.LinearVelocity, input.Gravity);
            double c = Numbers.Dot(input.LinearVelocity, input.LinearVelocity) - Limits.MaxLinearSpeed * Limits.MaxLinearSpeed;
            if (c == 0 && (b > 0 || (b == 0 && a > 0))) SetStop(0, StopReason.SpeedLimit);
            PlanRoots(a, b, c, StopReason.SpeedLimit);
            _consumed.Add(input.Intent);
            Current = Evaluate(0, _stopTime == 0);
            return Current;
        }

        public FlightState Step(double dt)
        {
            if (Current.Status != FlightStatus.Running) return Reject(RequestReason.NotRunning);
            if (!Numbers.Finite(dt) || dt <= 0 || Current.Elapsed + dt <= Current.Elapsed)
                return Reject(RequestReason.InvalidDeltaTime);
            if (dt > Limits.MaxDeltaTime)
            {
                Current = Copy(false, RequestReason.TimeGap, FlightStatus.Stopped, StopReason.TimeGap);
                return Current;
            }
            double requested = Current.Elapsed + dt;
            bool stopped = requested >= _stopTime;
            var next = Evaluate(Math.Min(requested, _stopTime), stopped);
            if (!Limits.Contains(next.Position) || !Numbers.Finite(next.LinearVelocity) || !Numbers.Finite(next.Orientation))
            {
                Current = Copy(false, RequestReason.InvalidLaunch, FlightStatus.Stopped, StopReason.NumericalFailure);
                return Current;
            }
            Current = next;
            return Current;
        }

        private FlightState Evaluate(double t, bool stopped)
        {
            var p = new Vector3((float)PositionAt(0, t), (float)PositionAt(1, t), (float)PositionAt(2, t));
            var v = new Vector3((float)(_launch.LinearVelocity.X + _launch.Gravity.X * t),
                (float)(_launch.LinearVelocity.Y + _launch.Gravity.Y * t), (float)(_launch.LinearVelocity.Z + _launch.Gravity.Z * t));
            // Only boundary roundoff is projected; the terminal time comes from the continuous trajectory.
            if (stopped && _plannedStop == StopReason.SpatialBoundary) p = Vector3.Clamp(p, Limits.Min, Limits.Max);
            double omega = Numbers.Length(_launch.AngularVelocity);
            var orientation = _initialOrientation;
            if (omega > 0)
            {
                double halfAngle = omega * t * 0.5;
                double scale = Math.Sin(halfAngle) / omega;
                var delta = new Quaternion((float)(_launch.AngularVelocity.X * scale), (float)(_launch.AngularVelocity.Y * scale),
                    (float)(_launch.AngularVelocity.Z * scale), (float)Math.Cos(halfAngle));
                Numbers.TryNormalize(delta * _initialOrientation, out orientation);
            }
            double gravity = Numbers.Length(_launch.Gravity);
            double upSpeed = gravity > 0 ? -Numbers.Dot(v, _launch.Gravity) / gravity : 0;
            var phase = gravity == 0 ? FlightPhase.Coasting : Math.Abs(upSpeed) <= 1e-6 ? FlightPhase.Apex
                : upSpeed > 0 ? FlightPhase.Rising : FlightPhase.Falling;
            return new FlightState(p, orientation, v, _launch.AngularVelocity, t,
                stopped ? FlightStatus.Stopped : FlightStatus.Running, phase,
                stopped ? _plannedStop : StopReason.None, _launch.Intent, true, RequestReason.Accepted);
        }

        private double PositionAt(int axis, double t) => Numbers.Axis(_launch.Position, axis)
            + Numbers.Axis(_launch.LinearVelocity, axis) * t + 0.5 * Numbers.Axis(_launch.Gravity, axis) * t * t;

        private void PlanBoundary(double position, double velocity, double acceleration, double boundary, bool upper)
        {
            double direction = upper ? 1 : -1;
            if (position == boundary && (direction * velocity > 0 || (velocity == 0 && direction * acceleration > 0)))
                SetStop(0, StopReason.SpatialBoundary);
            double distance = position - boundary;
            double virtualBoundary = position - distance;
            // Retain the low part of the subtraction so a tiny near-tangent clearance is not lost.
            double error = (position - (distance + virtualBoundary)) + (virtualBoundary - boundary);
            PlanRoots(0.5 * acceleration, velocity, distance, StopReason.SpatialBoundary, error);
        }

        private void PlanRoots(double a, double b, double c, StopReason reason, double cError = 0)
        {
            if (a == 0)
            {
                if (b != 0) ConsiderRoot(-c / b - cError / b, reason);
                return;
            }
            double discriminant = (b * b - 4 * a * c) - 4 * a * cError;
            if (discriminant < 0) return;
            double root = Math.Sqrt(discriminant);
            // This form avoids cancellation for one root; the other follows from their product c/a.
            double q = -0.5 * (b + (b >= 0 ? root : -root));
            if (q == 0) { ConsiderRoot(-b / (2 * a), reason); return; }
            ConsiderRoot(q / a, reason);
            ConsiderRoot(c / q + cError / q, reason);
        }

        private void ConsiderRoot(double time, StopReason reason)
        {
            if (time > 0 && Numbers.Finite(time)) SetStop(time, reason);
        }

        private void SetStop(double time, StopReason reason)
        {
            if (time <= _stopTime) { _stopTime = time; _plannedStop = reason; }
        }

        private FlightState Reject(RequestReason reason) => Copy(false, reason, Current.Status, Current.StopReason);
        private FlightState Copy(bool accepted, RequestReason reason, FlightStatus status, StopReason stop)
            => new FlightState(Current.Position, Current.Orientation, Current.LinearVelocity, Current.AngularVelocity, Current.Elapsed,
                status, Current.Phase, stop, Current.Intent, accepted, reason);
    }

    internal static class Numbers
    {
        public const double DomainLimit = 1e6;
        public const double MinLimit = 1e-6;
        public static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        public static bool Finite(Vector3 v) => Finite(v.X) && Finite(v.Y) && Finite(v.Z);
        public static bool Finite(Quaternion q) => Finite(q.X) && Finite(q.Y) && Finite(q.Z) && Finite(q.W);
        public static double Dot(Vector3 a, Vector3 b) => (double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z;
        public static double Length(Vector3 v) => Math.Sqrt(Dot(v, v));
        public static bool InDomain(Vector3 v) => Finite(v) && Math.Abs(v.X) <= DomainLimit && Math.Abs(v.Y) <= DomainLimit && Math.Abs(v.Z) <= DomainLimit;
        public static double Axis(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;
        public static bool TryNormalize(Quaternion q, out Quaternion normalized)
        {
            normalized = Quaternion.Identity;
            if (!Finite(q)) return false;
            double norm = Math.Sqrt((double)q.X * q.X + (double)q.Y * q.Y + (double)q.Z * q.Z + (double)q.W * q.W);
            if (norm < 1e-12) return false;
            normalized = new Quaternion((float)(q.X / norm), (float)(q.Y / norm), (float)(q.Z / norm), (float)(q.W / norm));
            return true;
        }
    }
}
