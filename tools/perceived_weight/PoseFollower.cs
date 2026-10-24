using System;
using System.Numerics;

namespace Toss.Experiments.PerceivedWeight
{
    public enum FollowMode { Rigid, SpringDamper }
    public enum SoundCondition { Off, On }

    [Flags]
    public enum FollowFlags { None = 0, LagLimited = 1, VelocityLimited = 2, TimeGapReset = 4 }

    public readonly struct FollowPose
    {
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }

        public FollowPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }

    public readonly struct FollowStep
    {
        public FollowPose Pose { get; }
        public FollowFlags Flags { get; }

        internal FollowStep(FollowPose pose, FollowFlags flags)
        {
            Pose = pose;
            Flags = flags;
        }
    }

    public sealed class ExperimentCondition
    {
        public FollowMode Follow { get; }
        public SoundCondition Sound { get; }
        public double GainNormalized { get; }
        public double EffectiveGain => Sound == SoundCondition.Off ? 0 : GainNormalized;

        public ExperimentCondition(FollowMode follow, SoundCondition sound, double gainNormalized = 0.1)
        {
            if (follow != FollowMode.Rigid && follow != FollowMode.SpringDamper)
                throw new ArgumentOutOfRangeException(nameof(follow));
            if (sound != SoundCondition.Off && sound != SoundCondition.On)
                throw new ArgumentOutOfRangeException(nameof(sound));
            FollowParameters.RequireRange(gainNormalized, 0, 1, nameof(gainNormalized));
            Follow = follow;
            Sound = sound;
            GainNormalized = gainNormalized;
        }
    }

    public sealed class FollowParameters
    {
        public double PositionFrequencyHz { get; }
        public double RotationFrequencyHz { get; }
        public double PositionDampingRatio { get; }
        public double RotationDampingRatio { get; }
        public double MaxPositionLagMeters { get; }
        public double MaxAngularLagRadians { get; }
        public double LinearVelocityLimit { get; }
        public double AngularVelocityLimit { get; }

        public FollowParameters(double positionFrequencyHz = 10, double rotationFrequencyHz = 8,
            double positionDampingRatio = 1, double rotationDampingRatio = 1,
            double maxPositionLagMeters = 0.004, double maxAngularLagRadians = Math.PI / 36,
            double linearVelocityLimit = 0.5, double angularVelocityLimit = 8)
        {
            RequireRange(positionFrequencyHz, 0.5, 30, nameof(positionFrequencyHz));
            RequireRange(rotationFrequencyHz, 0.5, 30, nameof(rotationFrequencyHz));
            RequireRange(positionDampingRatio, 0.5, 2, nameof(positionDampingRatio));
            RequireRange(rotationDampingRatio, 0.5, 2, nameof(rotationDampingRatio));
            RequireRange(maxPositionLagMeters, 0.000001, 0.02, nameof(maxPositionLagMeters));
            RequireRange(maxAngularLagRadians, 0.0001, Math.PI / 6, nameof(maxAngularLagRadians));
            RequireRange(linearVelocityLimit, 0.001, 3, nameof(linearVelocityLimit));
            RequireRange(angularVelocityLimit, 0.001, 20, nameof(angularVelocityLimit));
            PositionFrequencyHz = positionFrequencyHz;
            RotationFrequencyHz = rotationFrequencyHz;
            PositionDampingRatio = positionDampingRatio;
            RotationDampingRatio = rotationDampingRatio;
            MaxPositionLagMeters = maxPositionLagMeters;
            MaxAngularLagRadians = maxAngularLagRadians;
            LinearVelocityLimit = linearVelocityLimit;
            AngularVelocityLimit = angularVelocityLimit;
        }

        internal static void RequireRange(double value, double minimum, double maximum, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < minimum || value > maximum)
                throw new ArgumentOutOfRangeException(name, value, $"Expected [{minimum}, {maximum}].");
        }
    }

    public sealed class PoseFollower
    {
        public const double MaxDeltaTimeSeconds = 0.1;
        public const double MaxPositionCoordinateMeters = 1000;
        public FollowParameters Parameters { get; }
        public ExperimentCondition Condition { get; }
        public FollowPose OutputPose { get; private set; }
        public Vector3 LinearVelocity { get; private set; }
        public Vector3 AngularVelocity { get; private set; }

        public PoseFollower(FollowParameters parameters, ExperimentCondition condition, FollowPose initialPose)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            Reset(initialPose);
        }

        public void Reset(FollowPose initialPose)
        {
            var pose = ValidatedPose(initialPose, nameof(initialPose));
            OutputPose = pose;
            LinearVelocity = Vector3.Zero;
            AngularVelocity = Vector3.Zero;
        }

        // Feed back the prior output in the same reference frame; reset after external pose/frame changes.
        public FollowStep Step(FollowPose inputPose, FollowPose targetPose, double deltaTime)
        {
            var input = ValidatedPose(inputPose, nameof(inputPose));
            var target = ValidatedPose(targetPose, nameof(targetPose));
            FollowParameters.RequireRange(deltaTime, 0, double.MaxValue, nameof(deltaTime));
            if (deltaTime == 0)
            {
                OutputPose = input;
                return new FollowStep(OutputPose, FollowFlags.None);
            }
            if (deltaTime > MaxDeltaTimeSeconds)
            {
                Reset(target);
                return new FollowStep(OutputPose, FollowFlags.TimeGapReset);
            }
            if (Condition.Follow == FollowMode.Rigid)
            {
                Reset(target);
                return new FollowStep(OutputPose, FollowFlags.None);
            }

            var flags = FollowFlags.None;
            var positionError = input.Position - target.Position;
            var angularError = RotationVector(input.Rotation * Quaternion.Conjugate(target.Rotation));
            var linear = LinearVelocity;
            var angular = AngularVelocity;
            BoundLag(ref positionError, ref linear, Parameters.MaxPositionLagMeters, ref flags);
            BoundLag(ref angularError, ref angular, Parameters.MaxAngularLagRadians, ref flags);

            Advance(ref positionError, ref linear, Parameters.PositionFrequencyHz,
                Parameters.PositionDampingRatio, deltaTime);
            Advance(ref angularError, ref angular, Parameters.RotationFrequencyHz,
                Parameters.RotationDampingRatio, deltaTime);
            BoundLag(ref positionError, ref linear, Parameters.MaxPositionLagMeters, ref flags);
            BoundLag(ref angularError, ref angular, Parameters.MaxAngularLagRadians, ref flags);
            linear = LimitVelocity(linear, Parameters.LinearVelocityLimit, ref flags);
            angular = LimitVelocity(angular, Parameters.AngularVelocityLimit, ref flags);

            var position = target.Position + positionError;
            var boundedPosition = Vector3.Clamp(position,
                new Vector3(-(float)MaxPositionCoordinateMeters), new Vector3((float)MaxPositionCoordinateMeters));
            if (boundedPosition != position)
            {
                linear = Vector3.Zero;
                flags |= FollowFlags.LagLimited;
            }
            // Float reconstruction can exceed a tiny cap even when the solved error was bounded.
            if (Length(boundedPosition - target.Position) > Parameters.MaxPositionLagMeters)
            {
                boundedPosition = target.Position;
                linear = Vector3.Zero;
                flags |= FollowFlags.LagLimited;
            }
            var rotation = UnitQuaternion(FromRotationVector(angularError) * target.Rotation);
            if (Length(RotationVector(rotation * Quaternion.Conjugate(target.Rotation))) > Parameters.MaxAngularLagRadians)
            {
                rotation = target.Rotation;
                angular = Vector3.Zero;
                flags |= FollowFlags.LagLimited;
            }
            OutputPose = new FollowPose(boundedPosition, rotation);
            LinearVelocity = linear;
            AngularVelocity = angular;
            return new FollowStep(OutputPose, flags);
        }

        // Exact damped-oscillator step for a held target; rotation uses a small world-space tangent approximation.
        private static void Advance(ref Vector3 error, ref Vector3 velocity, double frequency, double damping, double dt)
        {
            double omega = 2 * Math.PI * frequency;
            double decay = Math.Exp(-damping * omega * dt);
            double cosine, sineOverFrequency;
            if (damping < 1)
            {
                double w = omega * Math.Sqrt(1 - damping * damping);
                cosine = Math.Cos(w * dt);
                sineOverFrequency = Math.Sin(w * dt) / w;
            }
            else if (damping > 1)
            {
                double w = omega * Math.Sqrt(damping * damping - 1);
                cosine = Math.Cosh(w * dt);
                sineOverFrequency = Math.Sinh(w * dt) / w;
            }
            else
            {
                cosine = 1;
                sineOverFrequency = dt;
            }
            double ee = decay * (cosine + damping * omega * sineOverFrequency);
            double ev = decay * sineOverFrequency;
            double ve = -decay * omega * omega * sineOverFrequency;
            double vv = decay * (cosine - damping * omega * sineOverFrequency);
            var nextError = Combine(error, ee, velocity, ev);
            velocity = Combine(error, ve, velocity, vv);
            error = nextError;
        }

        private static Vector3 Combine(Vector3 a, double aScale, Vector3 b, double bScale)
        {
            return new Vector3((float)(a.X * aScale + b.X * bScale),
                (float)(a.Y * aScale + b.Y * bScale), (float)(a.Z * aScale + b.Z * bScale));
        }

        // Projection may jump after a discontinuity; velocity limits bound state, not this corrective displacement.
        private static void BoundLag(ref Vector3 error, ref Vector3 velocity, double maximum, ref FollowFlags flags)
        {
            double length = Length(error);
            if (length <= maximum) return;
            error = Combine(error, maximum / length, Vector3.Zero, 0);
            velocity = Vector3.Zero;
            flags |= FollowFlags.LagLimited;
        }

        private static Vector3 LimitVelocity(Vector3 velocity, double maximum, ref FollowFlags flags)
        {
            double length = Length(velocity);
            if (length <= maximum) return velocity;
            flags |= FollowFlags.VelocityLimited;
            return Combine(velocity, maximum / length, Vector3.Zero, 0);
        }

        private static double Length(Vector3 value)
        {
            return Math.Sqrt((double)value.X * value.X + (double)value.Y * value.Y + (double)value.Z * value.Z);
        }

        private static FollowPose ValidatedPose(FollowPose pose, string name)
        {
            FollowParameters.RequireRange(pose.Position.X, -MaxPositionCoordinateMeters, MaxPositionCoordinateMeters, name);
            FollowParameters.RequireRange(pose.Position.Y, -MaxPositionCoordinateMeters, MaxPositionCoordinateMeters, name);
            FollowParameters.RequireRange(pose.Position.Z, -MaxPositionCoordinateMeters, MaxPositionCoordinateMeters, name);
            var q = pose.Rotation;
            double norm = (double)q.X * q.X + (double)q.Y * q.Y + (double)q.Z * q.Z + (double)q.W * q.W;
            if (double.IsNaN(norm) || double.IsInfinity(norm) || norm < 1e-12)
                throw new ArgumentException("Expected a finite, non-degenerate quaternion (norm >= 1e-6).", name);
            return new FollowPose(pose.Position, UnitQuaternion(q));
        }

        private static Quaternion UnitQuaternion(Quaternion q)
        {
            double inverseNorm = 1 / Math.Sqrt((double)q.X * q.X + (double)q.Y * q.Y + (double)q.Z * q.Z + (double)q.W * q.W);
            return new Quaternion((float)(q.X * inverseNorm), (float)(q.Y * inverseNorm),
                (float)(q.Z * inverseNorm), (float)(q.W * inverseNorm));
        }

        private static Vector3 RotationVector(Quaternion rotation)
        {
            var q = UnitQuaternion(rotation);
            // Resolve the exact half-turn tie lexicographically so q and -q take the same branch.
            if (q.W < 0 || (q.W == 0 && (q.X < 0 || (q.X == 0 && (q.Y < 0 || (q.Y == 0 && q.Z < 0))))))
                q = new Quaternion(-q.X, -q.Y, -q.Z, -q.W);
            var vector = new Vector3(q.X, q.Y, q.Z);
            double length = Length(vector);
            if (length < 1e-12) return vector * 2;
            return Combine(vector, 2 * Math.Atan2(length, q.W) / length, Vector3.Zero, 0);
        }

        private static Quaternion FromRotationVector(Vector3 vector)
        {
            double angle = Length(vector);
            double scale = angle < 1e-12 ? 0.5 : Math.Sin(angle / 2) / angle;
            return new Quaternion((float)(vector.X * scale), (float)(vector.Y * scale),
                (float)(vector.Z * scale), (float)Math.Cos(angle / 2));
        }
    }
}
