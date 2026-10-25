// P1.4 ISOLATED PROTOTYPE | NOT INTEGRATED | NOT PERCEPTUALLY VALIDATED
// Low-energy settle reachability assistance: a deterministic, pure .NET correction-proposal solver.
// It never writes a Transform, Rigidbody, pointer pose or SDK state. All numbers are experiment values.
using System;
using System.Numerics;

namespace Toss.Experiments.InvisibleGenerosity
{
    public enum AssistanceProfile { Off, Subtle, Obvious }

    // Mirrors the production FingerPlayState names plus the two authority states that must veto assistance.
    public enum InteractionState { Showcase, Grabbed, Settling, Settled, Cancelled, RecoveryLocked }

    public enum AssistReason
    {
        ProfileOff,
        NotInSettleWindow,
        InvalidInput,
        InvalidDeltaTime,
        TimeGap,
        NoTrustedTarget,
        HighEnergy,
        OutOfAssistRange,
        InsideSafeRegion,
        TimeBudgetExhausted,
        DisplacementBudgetExhausted,
        Assisting
    }

    [Flags]
    public enum AssistLimits
    {
        None = 0,
        VelocityLimited = 1,
        AccelerationLimited = 2,
        OvershootGuard = 4,
        DisplacementBudgetLimited = 8,
        TimeBudgetLimited = 16,
        HardClamped = 32,
        HardDisabled = 64
    }

    public readonly struct ReferenceRegion
    {
        public Vector3 Center { get; }
        public bool IsTrusted { get; }
        public ReferenceRegion(Vector3 center, bool isTrusted) { Center = center; IsTrusted = isTrusted; }
        public static ReferenceRegion None => new ReferenceRegion(Vector3.Zero, false);
    }

    public readonly struct GenerosityInput
    {
        public Vector3 CoinPosition { get; }
        public Vector3 CoinVelocity { get; }
        public ReferenceRegion Reference { get; }
        public double DeltaTime { get; }
        public InteractionState State { get; }

        public GenerosityInput(Vector3 coinPosition, Vector3 coinVelocity, ReferenceRegion reference, double deltaTime,
            InteractionState state)
        {
            CoinPosition = coinPosition;
            CoinVelocity = coinVelocity;
            Reference = reference;
            DeltaTime = deltaTime;
            State = state;
        }
    }

    // PositionDelta and VelocityDelta describe the SAME correction in two forms; a consumer must apply exactly one.
    public readonly struct GenerosityProposal
    {
        public bool Enabled { get; }
        public Vector3 PositionDelta { get; }
        public Vector3 VelocityDelta { get; }
        public Vector3 AssistVelocity { get; }
        public AssistReason Reason { get; }
        public AssistLimits Limits { get; }
        public double DisplacementUsedMeters { get; }
        public double DisplacementRemainingMeters { get; }
        public double TimeUsedSeconds { get; }
        public double Gain { get; }

        public GenerosityProposal(bool enabled, Vector3 positionDelta, Vector3 velocityDelta, Vector3 assistVelocity,
            AssistReason reason, AssistLimits limits, double used, double remaining, double timeUsed, double gain)
        {
            Enabled = enabled;
            PositionDelta = positionDelta;
            VelocityDelta = velocityDelta;
            AssistVelocity = assistVelocity;
            Reason = reason;
            Limits = limits;
            DisplacementUsedMeters = used;
            DisplacementRemainingMeters = remaining;
            TimeUsedSeconds = timeUsed;
            Gain = gain;
        }
    }

    public sealed class GenerosityParameters
    {
        public double SafeRadiusMeters { get; }          // inside: no assistance
        public double FullGainRadiusMeters { get; }      // gain reaches 1 here
        public double MaxAssistRangeMeters { get; }      // beyond: not a "slightly out of reach" settle
        public double MaxAssistSpeed { get; }            // m/s, cap on injected velocity
        public double MaxAssistAcceleration { get; }     // m/s^2, cap on per-step change of injected velocity
        public double DisplacementBudgetMeters { get; }  // total injected displacement per settle episode
        public double TimeBudgetSeconds { get; }
        public double LowEnergySpeedMax { get; }         // coin speed above this is not a low-energy release
        public double MaxDeltaTimeSeconds { get; }

        public GenerosityParameters(double safeRadiusMeters, double fullGainRadiusMeters, double maxAssistRangeMeters,
            double maxAssistSpeed, double maxAssistAcceleration, double displacementBudgetMeters, double timeBudgetSeconds,
            double lowEnergySpeedMax, double maxDeltaTimeSeconds = 0.1)
        {
            RequireFinitePositive(safeRadiusMeters, nameof(safeRadiusMeters));
            RequireFinitePositive(fullGainRadiusMeters, nameof(fullGainRadiusMeters));
            RequireFinitePositive(maxAssistRangeMeters, nameof(maxAssistRangeMeters));
            RequireFinitePositive(maxAssistSpeed, nameof(maxAssistSpeed));
            RequireFinitePositive(maxAssistAcceleration, nameof(maxAssistAcceleration));
            RequireFinitePositive(displacementBudgetMeters, nameof(displacementBudgetMeters));
            RequireFinitePositive(timeBudgetSeconds, nameof(timeBudgetSeconds));
            RequireFinitePositive(lowEnergySpeedMax, nameof(lowEnergySpeedMax));
            RequireFinitePositive(maxDeltaTimeSeconds, nameof(maxDeltaTimeSeconds));
            if (!(safeRadiusMeters < fullGainRadiusMeters && fullGainRadiusMeters <= maxAssistRangeMeters))
                throw new ArgumentException("Expected safe < fullGain <= maxAssistRange radii.");
            SafeRadiusMeters = safeRadiusMeters;
            FullGainRadiusMeters = fullGainRadiusMeters;
            MaxAssistRangeMeters = maxAssistRangeMeters;
            MaxAssistSpeed = maxAssistSpeed;
            MaxAssistAcceleration = maxAssistAcceleration;
            DisplacementBudgetMeters = displacementBudgetMeters;
            TimeBudgetSeconds = timeBudgetSeconds;
            LowEnergySpeedMax = lowEnergySpeedMax;
            MaxDeltaTimeSeconds = maxDeltaTimeSeconds;
        }

        // Experiment values only. Not product thresholds.
        public static GenerosityParameters Subtle() => new GenerosityParameters(
            safeRadiusMeters: 0.12, fullGainRadiusMeters: 0.30, maxAssistRangeMeters: 0.60,
            maxAssistSpeed: 0.05, maxAssistAcceleration: 0.30, displacementBudgetMeters: 0.03,
            timeBudgetSeconds: 0.75, lowEnergySpeedMax: 0.5);

        public static GenerosityParameters Obvious() => new GenerosityParameters(
            safeRadiusMeters: 0.12, fullGainRadiusMeters: 0.30, maxAssistRangeMeters: 0.60,
            maxAssistSpeed: 0.15, maxAssistAcceleration: 1.0, displacementBudgetMeters: 0.10,
            timeBudgetSeconds: 1.5, lowEnergySpeedMax: 0.5);

        private static void RequireFinitePositive(double v, string name)
        {
            if (double.IsNaN(v) || double.IsInfinity(v) || v <= 0)
                throw new ArgumentOutOfRangeException(name, "Expected a finite positive value.");
        }
    }

    public sealed class GenerositySolver
    {
        public const double MaxCoordinateMeters = 1000;
        public const double DisplacementEpsilonMeters = 1e-6;
        public AssistanceProfile Profile { get; }
        public GenerosityParameters Parameters { get; }
        public Vector3 AssistVelocity { get; private set; }
        public double DisplacementUsedMeters { get; private set; }
        public double TimeUsedSeconds { get; private set; }
        public bool EpisodeActive { get; private set; }

        public GenerositySolver(AssistanceProfile profile, GenerosityParameters parameters)
        {
            Profile = profile;
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Reset();
        }

        public void Reset()
        {
            AssistVelocity = Vector3.Zero;
            DisplacementUsedMeters = 0;
            TimeUsedSeconds = 0;
            EpisodeActive = false;
        }

        public GenerosityProposal Step(GenerosityInput input)
        {
            if (Profile == AssistanceProfile.Off)
                return Zero(AssistReason.ProfileOff);

            if (input.State != InteractionState.Settling)
            {
                // Authority changed (hand, cancel, recovery lock, rest): the settle episode is over and
                // nothing may carry into the next one.
                EpisodeActive = false;
                return HardDisable(AssistReason.NotInSettleWindow);
            }

            if (!EpisodeActive)
            {
                EpisodeActive = true;
                AssistVelocity = Vector3.Zero;
                DisplacementUsedMeters = 0;
                TimeUsedSeconds = 0;
            }

            double dt = input.DeltaTime;
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt < 0)
                return Zero(AssistReason.InvalidDeltaTime);
            if (dt == 0)
                return Zero(AssistReason.InvalidDeltaTime);
            if (dt > Parameters.MaxDeltaTimeSeconds)
                return HardDisable(AssistReason.TimeGap);
            if (!IsFinite(input.CoinPosition) || !IsFinite(input.CoinVelocity))
                return HardDisable(AssistReason.InvalidInput);

            TimeUsedSeconds += dt;

            // Without a trusted target there is nothing to brake toward; braking along the stale direction
            // would still be assistance toward the old target, so this is a hard disable, not a ramp.
            if (!input.Reference.IsTrusted || !IsFinite(input.Reference.Center))
                return HardDisable(AssistReason.NoTrustedTarget);

            // Desired injected velocity (zero means "ramp down").
            var limits = AssistLimits.None;
            double gain = 0;
            Vector3 desired = Vector3.Zero;
            AssistReason reason;
            if (Length(input.CoinVelocity) > Parameters.LowEnergySpeedMax)
                reason = AssistReason.HighEnergy;
            else if (TimeUsedSeconds > Parameters.TimeBudgetSeconds)
            {
                reason = AssistReason.TimeBudgetExhausted;
                limits |= AssistLimits.TimeBudgetLimited;
            }
            else
            {
                var toCenter = input.Reference.Center - input.CoinPosition;
                double distance = Length(toCenter);
                if (distance > Parameters.MaxAssistRangeMeters)
                    reason = AssistReason.OutOfAssistRange;
                else if (distance <= Parameters.SafeRadiusMeters)
                    reason = AssistReason.InsideSafeRegion;
                else
                {
                    reason = AssistReason.Assisting;
                    gain = SmoothStep((distance - Parameters.SafeRadiusMeters)
                                      / (Parameters.FullGainRadiusMeters - Parameters.SafeRadiusMeters));
                    double speed = gain * Parameters.MaxAssistSpeed;
                    // Arrive at the safe boundary with zero injected velocity (braking distance bound).
                    double overshootBound = BrakingSpeedBound(distance - Parameters.SafeRadiusMeters, dt);
                    if (speed > overshootBound) { speed = overshootBound; limits |= AssistLimits.OvershootGuard; }
                    // Keep enough displacement budget to brake to zero.
                    double remaining = Math.Max(0, Parameters.DisplacementBudgetMeters - DisplacementUsedMeters);
                    // The conservative braking bound converges to the budget from below; treat sub-micron slack as spent.
                    if (remaining <= DisplacementEpsilonMeters)
                    {
                        speed = 0;
                        reason = AssistReason.DisplacementBudgetExhausted;
                        limits |= AssistLimits.DisplacementBudgetLimited;
                    }
                    else
                    {
                        double budgetBound = BrakingSpeedBound(remaining, dt);
                        if (speed > budgetBound) { speed = budgetBound; limits |= AssistLimits.DisplacementBudgetLimited; }
                    }
                    desired = toCenter * (float)(speed / distance);
                }
            }

            // Acceleration-limited approach of the injected velocity toward the desired value.
            var change = desired - AssistVelocity;
            double changeLength = Length(change);
            double maxChange = Parameters.MaxAssistAcceleration * dt;
            if (changeLength > maxChange)
            {
                change *= (float)(maxChange / changeLength);
                limits |= AssistLimits.AccelerationLimited;
            }
            var velocity = AssistVelocity + change;
            double velocityLength = Length(velocity);
            if (velocityLength > Parameters.MaxAssistSpeed)
            {
                velocity *= (float)(Parameters.MaxAssistSpeed / velocityLength);
                limits |= AssistLimits.VelocityLimited;
            }
            if (velocityLength < 1e-6) velocity = Vector3.Zero;

            var positionDelta = velocity * (float)dt;
            double stepLength = Length(positionDelta);
            double budgetLeft = Parameters.DisplacementBudgetMeters - DisplacementUsedMeters;
            if (stepLength > budgetLeft)
            {
                // Should be unreachable thanks to the braking bound; kept as a hard guarantee.
                double scale = budgetLeft <= 0 ? 0 : budgetLeft / stepLength;
                positionDelta *= (float)scale;
                velocity = Vector3.Zero;
                stepLength = Length(positionDelta);
                limits |= AssistLimits.HardClamped;
                reason = AssistReason.DisplacementBudgetExhausted;
            }

            var velocityDelta = velocity - AssistVelocity;
            AssistVelocity = velocity;
            DisplacementUsedMeters += stepLength;

            // Enabled with a non-Assisting reason means the injected velocity is ramping down to zero.
            bool enabled = positionDelta != Vector3.Zero;
            return new GenerosityProposal(enabled, positionDelta, velocityDelta, AssistVelocity, reason, limits,
                DisplacementUsedMeters, Math.Max(0, Parameters.DisplacementBudgetMeters - DisplacementUsedMeters),
                TimeUsedSeconds, gain);
        }

        private GenerosityProposal Zero(AssistReason reason)
        {
            return new GenerosityProposal(false, Vector3.Zero, Vector3.Zero, AssistVelocity, reason, AssistLimits.None,
                DisplacementUsedMeters, Math.Max(0, Parameters.DisplacementBudgetMeters - DisplacementUsedMeters),
                TimeUsedSeconds, 0);
        }

        // Safety gate: the injected velocity is abandoned, not braked. This proposal is NOT a retraction of
        // velocity a consumer may already have applied; it only stops proposing more.
        private GenerosityProposal HardDisable(AssistReason reason)
        {
            AssistVelocity = Vector3.Zero;
            return new GenerosityProposal(false, Vector3.Zero, Vector3.Zero, Vector3.Zero, reason, AssistLimits.HardDisabled,
                DisplacementUsedMeters, Math.Max(0, Parameters.DisplacementBudgetMeters - DisplacementUsedMeters),
                TimeUsedSeconds, 0);
        }

        // Largest speed from which a constant-deceleration ramp sampled at dt stops within `distance`.
        // The sampled sum s*dt + (s-a*dt)*dt + ... is bounded above by s^2/(2a) + s*dt for any step count.
        private double BrakingSpeedBound(double distance, double dt)
        {
            if (distance <= 0) return 0;
            double a = Parameters.MaxAssistAcceleration;
            double step = a * dt;
            return Math.Sqrt(step * step + 2 * a * distance) - step;
        }

        private static double SmoothStep(double x)
        {
            if (x <= 0) return 0;
            if (x >= 1) return 1;
            return x * x * (3 - 2 * x);
        }

        private static double Length(Vector3 v)
        {
            return Math.Sqrt((double)v.X * v.X + (double)v.Y * v.Y + (double)v.Z * v.Z);
        }

        private static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z)
                && Math.Abs(v.X) <= MaxCoordinateMeters && Math.Abs(v.Y) <= MaxCoordinateMeters && Math.Abs(v.Z) <= MaxCoordinateMeters;
        }

        private static bool IsFinite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    }
}
