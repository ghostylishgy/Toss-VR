// P2 TOSS INTENT ISOLATED PROTOTYPE | NOT INTEGRATED | NOT RUNTIME VALIDATED | NO RNG / NO PHYSICS FLIGHT
// Deterministic event-driven gate that separates a Finger Play release from an intentional upward toss.
// It only proposes a TossIntent event. It never generates a result, calls RNG, writes a Rigidbody, moves a
// Transform or drives Catch. "Flight" here is a legal logical transition, not a running physics flight.
// All thresholds are experiment parameters, not frozen product values.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Toss.Experiments.TossIntent
{
    // The four formal states this round may use (PROJECT_BRIEF section 5.4). Catch/Recovery/Reveal are out of scope;
    // Flight is left only by Reset.
    public enum GateState { Ready, Grabbed, Armed, Flight }

    public enum GateEventKind { GrabBegin, MotionSample, Release, Cancel, Reset }

    public enum GateReason
    {
        None,
        Transition,            // plain legal transition (GrabBegin, Reset)
        IllegalTransition,     // event not legal in the current state; state unchanged
        InvalidTimestamp,      // NaN/Inf, or not strictly after the previous event
        InvalidSample,         // non-finite or implausible position / velocity
        FrameMismatch,         // sample not expressed in the configured motion frame
        OwnershipInvalid,      // sample not backed by a valid grab owner (tracking lost)
        ContinuityBroken,      // gap between samples too large
        EvidenceBuilding,      // sample accepted, Armed conditions not met yet
        Armed,                 // Grabbed -> Armed on this sample
        ArmedSustained,        // Armed, newest sample still meets the sustain floor
        ArmedHeld,             // Armed, newest sample does not qualify, held only by HoldForWindow policy
        Disarmed,              // Armed -> Grabbed (evidence lost)
        FingerPlayRelease,     // Release without a valid toss intent -> Ready
        ReleaseTooLate,        // Armed, but release came after the confirmation window -> Ready, no intent
        TossIntentAccepted,    // Armed -> Flight, exactly one intent proposed
        DuplicateRelease,      // Release while already in Flight
        Cancelled,
        Reset
    }

    // How Armed behaves once the latest sample no longer qualifies. Both are research candidates; see the round report.
    public enum ArmedHoldPolicy
    {
        RevokeOnEvidenceLoss,  // Armed exists only while the newest sample still qualifies
        HoldForWindow          // Armed persists up to ArmedHoldSeconds after the last qualifying sample
    }

    // The frame in which MotionSample position/velocity are expressed. Only samples whose FrameId matches the
    // gate's ExpectedFrameId are consumed; Up must be a finite unit vector.
    public readonly struct MotionFrame
    {
        public int FrameId { get; }
        public Vector3 Up { get; }
        public MotionFrame(int frameId, Vector3 up) { FrameId = frameId; Up = up; }
        public static MotionFrame WorldYUp => new MotionFrame(0, new Vector3(0f, 1f, 0f));
    }

    public readonly struct GateEvent
    {
        public GateEventKind Kind { get; }
        public double Time { get; }
        public Vector3 Position { get; }
        public Vector3 Velocity { get; }
        public MotionFrame Frame { get; }
        public bool OwnershipValid { get; }

        private GateEvent(GateEventKind kind, double time, Vector3 position, Vector3 velocity, MotionFrame frame, bool ownershipValid)
        {
            Kind = kind; Time = time; Position = position; Velocity = velocity; Frame = frame; OwnershipValid = ownershipValid;
        }

        public static GateEvent GrabBegin(double time) => new GateEvent(GateEventKind.GrabBegin, time, Vector3.Zero, Vector3.Zero, default, true);
        public static GateEvent Sample(double time, Vector3 position, Vector3 velocity, MotionFrame frame, bool ownershipValid = true)
            => new GateEvent(GateEventKind.MotionSample, time, position, velocity, frame, ownershipValid);
        public static GateEvent Release(double time) => new GateEvent(GateEventKind.Release, time, Vector3.Zero, Vector3.Zero, default, true);
        public static GateEvent Cancel(double time) => new GateEvent(GateEventKind.Cancel, time, Vector3.Zero, Vector3.Zero, default, true);
        public static GateEvent Reset() => new GateEvent(GateEventKind.Reset, double.NaN, Vector3.Zero, Vector3.Zero, default, true);
    }

    // Diagnostic evidence over the current sustained-upward run. Evidence alone is never an intent.
    public readonly struct MotionEvidence
    {
        public int SampleCount { get; }
        public double SustainedSeconds { get; }
        public double MeanUpwardSpeed { get; }
        public double PeakUpwardSpeed { get; }
        public double LatestUpwardSpeed { get; }
        public double MinUpwardRatio { get; }
        public double UpwardDisplacement { get; }
        public double LastQualifyingTime { get; }

        public MotionEvidence(int count, double sustained, double mean, double peak, double latest, double minRatio,
            double displacement, double lastQualifying)
        {
            SampleCount = count; SustainedSeconds = sustained; MeanUpwardSpeed = mean; PeakUpwardSpeed = peak;
            LatestUpwardSpeed = latest; MinUpwardRatio = minRatio; UpwardDisplacement = displacement; LastQualifyingTime = lastQualifying;
        }

        public static MotionEvidence Empty => new MotionEvidence(0, 0, 0, 0, 0, 0, 0, double.NaN);
    }

    // The only thing the gate may emit: a proposal that a toss was intended. No result, no physics.
    public readonly struct TossIntent
    {
        public int IntentId { get; }          // equals the grab sequence; one grab can yield at most one intent
        public int GrabSequence { get; }
        public double ArmedTime { get; }
        public double ReleaseTime { get; }
        public MotionEvidence Evidence { get; }

        public TossIntent(int intentId, int grabSequence, double armedTime, double releaseTime, MotionEvidence evidence)
        {
            IntentId = intentId; GrabSequence = grabSequence; ArmedTime = armedTime; ReleaseTime = releaseTime; Evidence = evidence;
        }
    }

    public readonly struct GateResult
    {
        public GateState State { get; }
        public bool IsArmed => State == GateState.Armed;
        public bool EventAccepted { get; }
        public bool TossIntentAccepted { get; }
        public GateReason Reason { get; }
        public MotionEvidence Evidence { get; }
        public TossIntent? Intent { get; }
        public int GrabSequence { get; }
        public long EventSequence { get; }

        public GateResult(GateState state, bool eventAccepted, GateReason reason, MotionEvidence evidence, TossIntent? intent,
            int grabSequence, long eventSequence)
        {
            State = state; EventAccepted = eventAccepted; Reason = reason; Evidence = evidence; Intent = intent;
            TossIntentAccepted = intent.HasValue; GrabSequence = grabSequence; EventSequence = eventSequence;
        }
    }

    public sealed class TossIntentParameters
    {
        public int ExpectedFrameId { get; }
        public double SustainUpwardSpeed { get; }      // m/s; a sample below this ends the sustained run
        public double ArmMeanUpwardSpeed { get; }      // m/s; mean over the run must reach this to arm
        public double MinUpwardRatio { get; }          // up / |v| for every sample of the run (direction, rejects lateral)
        public double MinEvidenceSeconds { get; }      // run span required before arming
        public int MinEvidenceSamples { get; }
        public double MinUpwardDisplacement { get; }   // m, from positions, independent of reported velocity
        public double MaxSampleGapSeconds { get; }     // larger gap breaks continuity
        public double ReleaseGraceSeconds { get; }     // Release must follow the last qualifying sample within this
        public ArmedHoldPolicy HoldPolicy { get; }
        public double ArmedHoldSeconds { get; }        // HoldForWindow only
        public double MaxPlausibleSpeed { get; }       // m/s; above this the sample is a tracking glitch
        public double MaxCoordinate { get; }

        public TossIntentParameters(int expectedFrameId, double sustainUpwardSpeed, double armMeanUpwardSpeed, double minUpwardRatio,
            double minEvidenceSeconds, int minEvidenceSamples, double minUpwardDisplacement, double maxSampleGapSeconds,
            double releaseGraceSeconds, ArmedHoldPolicy holdPolicy, double armedHoldSeconds,
            double maxPlausibleSpeed = 20, double maxCoordinate = 1000)
        {
            FinitePositive(sustainUpwardSpeed, nameof(sustainUpwardSpeed));
            FinitePositive(armMeanUpwardSpeed, nameof(armMeanUpwardSpeed));
            FinitePositive(minEvidenceSeconds, nameof(minEvidenceSeconds));
            FinitePositive(minUpwardDisplacement, nameof(minUpwardDisplacement));
            FinitePositive(maxSampleGapSeconds, nameof(maxSampleGapSeconds));
            FinitePositive(releaseGraceSeconds, nameof(releaseGraceSeconds));
            FinitePositive(armedHoldSeconds, nameof(armedHoldSeconds));
            FinitePositive(maxPlausibleSpeed, nameof(maxPlausibleSpeed));
            FinitePositive(maxCoordinate, nameof(maxCoordinate));
            if (double.IsNaN(minUpwardRatio) || minUpwardRatio <= 0 || minUpwardRatio > 1)
                throw new ArgumentOutOfRangeException(nameof(minUpwardRatio));
            if (minEvidenceSamples < 2) throw new ArgumentOutOfRangeException(nameof(minEvidenceSamples));
            if (armMeanUpwardSpeed < sustainUpwardSpeed) throw new ArgumentException("Expected armMeanUpwardSpeed >= sustainUpwardSpeed.");

            ExpectedFrameId = expectedFrameId;
            SustainUpwardSpeed = sustainUpwardSpeed;
            ArmMeanUpwardSpeed = armMeanUpwardSpeed;
            MinUpwardRatio = minUpwardRatio;
            MinEvidenceSeconds = minEvidenceSeconds;
            MinEvidenceSamples = minEvidenceSamples;
            MinUpwardDisplacement = minUpwardDisplacement;
            MaxSampleGapSeconds = maxSampleGapSeconds;
            ReleaseGraceSeconds = releaseGraceSeconds;
            HoldPolicy = holdPolicy;
            ArmedHoldSeconds = armedHoldSeconds;
            MaxPlausibleSpeed = maxPlausibleSpeed;
            MaxCoordinate = maxCoordinate;
        }

        // Experiment values only. Not product thresholds.
        public static TossIntentParameters Experimental(ArmedHoldPolicy policy = ArmedHoldPolicy.RevokeOnEvidenceLoss,
            double armedHoldSeconds = 0.15) => new TossIntentParameters(
            expectedFrameId: 0, sustainUpwardSpeed: 0.25, armMeanUpwardSpeed: 0.60, minUpwardRatio: 0.5,
            minEvidenceSeconds: 0.06, minEvidenceSamples: 3, minUpwardDisplacement: 0.04, maxSampleGapSeconds: 0.05,
            releaseGraceSeconds: 0.10, holdPolicy: policy, armedHoldSeconds: armedHoldSeconds);

        private static void FinitePositive(double v, string name)
        {
            if (double.IsNaN(v) || double.IsInfinity(v) || v <= 0) throw new ArgumentOutOfRangeException(name);
        }
    }

    public sealed class TossIntentGate
    {
        private struct Sample
        {
            public double Time, UpSpeed, Ratio;
            public Vector3 Position, FrameUp;
        }

        // Absorbs floating-point accumulation of frame times at the continuity boundary.
        public const double TimeToleranceSeconds = 1e-6;
        public TossIntentParameters Parameters { get; }
        public GateState State { get; private set; }
        public int GrabSequence { get; private set; }
        public long EventSequence { get; private set; }
        public int IntentsAccepted { get; private set; }

        private readonly List<Sample> _run = new List<Sample>();   // current sustained-upward run, oldest first
        private double _lastEventTime = double.NegativeInfinity;
        private double _lastSampleTime = double.NaN;
        private Vector3? _grabUp;
        private double _armedTime = double.NaN;
        private bool _intentIssuedThisGrab;
        private MotionEvidence _evidence = MotionEvidence.Empty;
        private MotionEvidence _armingEvidence = MotionEvidence.Empty;
        private int _armingGrabSequence;

        public TossIntentGate(TossIntentParameters parameters)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            State = GateState.Ready;
        }

        public GateResult Process(GateEvent e)
        {
            EventSequence++;
            switch (e.Kind)
            {
                case GateEventKind.Reset: return DoReset();
                case GateEventKind.GrabBegin: return DoGrabBegin(e);
                case GateEventKind.MotionSample: return DoSample(e);
                case GateEventKind.Release: return DoRelease(e);
                case GateEventKind.Cancel: return DoCancel(e);
                default: return Result(false, GateReason.IllegalTransition, null);
            }
        }

        private GateResult DoReset()
        {
            State = GateState.Ready;
            ClearEvidence();
            _intentIssuedThisGrab = false;
            _lastEventTime = double.NegativeInfinity;
            _lastSampleTime = double.NaN;
            _grabUp = null;
            return Result(true, GateReason.Reset, null);
        }

        private GateResult DoGrabBegin(GateEvent e)
        {
            if (State != GateState.Ready) return Result(false, GateReason.IllegalTransition, null);
            if (!AcceptTime(e.Time, allowEqual: true)) return Result(false, GateReason.InvalidTimestamp, null);
            State = GateState.Grabbed;
            GrabSequence++;
            ClearEvidence();
            _lastSampleTime = double.NaN;
            _grabUp = null;
            _intentIssuedThisGrab = false;
            return Result(true, GateReason.Transition, null);
        }

        private GateResult DoCancel(GateEvent e)
        {
            // Cancel is an authority event: it always wins over motion evidence while the coin is held.
            if (State != GateState.Grabbed && State != GateState.Armed) return Result(false, GateReason.IllegalTransition, null);
            AcceptTime(e.Time, allowEqual: true); // a bad clock does not keep the coin held
            State = GateState.Ready;
            ClearEvidence();
            return Result(true, GateReason.Cancelled, null);
        }

        private GateResult DoRelease(GateEvent e)
        {
            if (State == GateState.Flight) return Result(false, GateReason.DuplicateRelease, null);
            if (State != GateState.Grabbed && State != GateState.Armed) return Result(false, GateReason.IllegalTransition, null);

            bool timeOk = AcceptTime(e.Time, allowEqual: true);
            if (State == GateState.Armed && timeOk && !_intentIssuedThisGrab)
            {
                if (_armingGrabSequence == GrabSequence && ValidEvidence(_armingEvidence)
                    && Finite(_armedTime) && _armedTime <= e.Time && WithinReleaseWindow(e.Time))
                {
                    var intent = new TossIntent(GrabSequence, GrabSequence, _armedTime, e.Time, _armingEvidence);
                    _intentIssuedThisGrab = true;
                    IntentsAccepted++;
                    State = GateState.Flight;
                    var accepted = new GateResult(State, true, GateReason.TossIntentAccepted, _armingEvidence, intent, GrabSequence, EventSequence);
                    ClearEvidence();
                    return accepted;
                }
                State = GateState.Ready;
                ClearEvidence();
                return Result(true, GateReason.ReleaseTooLate, null);
            }

            // Grabbed, Armed with a broken clock, or (defensively) a second intent in the same grab: plain finger play release.
            State = GateState.Ready;
            ClearEvidence();
            return Result(true, timeOk ? GateReason.FingerPlayRelease : GateReason.InvalidTimestamp, null);
        }

        private GateResult DoSample(GateEvent e)
        {
            if (State != GateState.Grabbed && State != GateState.Armed) return Result(false, GateReason.IllegalTransition, null);

            double prevTime = _lastSampleTime;
            if (!AcceptTime(e.Time, allowEqual: false)) return Reject(GateReason.InvalidTimestamp);
            _lastSampleTime = e.Time;
            if (!e.OwnershipValid) return Reject(GateReason.OwnershipInvalid);
            if (e.Frame.FrameId != Parameters.ExpectedFrameId || !IsUnit(e.Frame.Up)
                || (_grabUp.HasValue && e.Frame.Up != _grabUp.Value)) return Reject(GateReason.FrameMismatch);
            if (!IsFinite(e.Position, Parameters.MaxCoordinate) || !IsFinite(e.Velocity, Parameters.MaxPlausibleSpeed))
                return Reject(GateReason.InvalidSample);
            _grabUp = e.Frame.Up;

            if (Finite(prevTime) && e.Time - prevTime > Parameters.MaxSampleGapSeconds + TimeToleranceSeconds)
                return Reject(GateReason.ContinuityBroken);

            bool expired = State == GateState.Armed && !WithinReleaseWindow(e.Time);
            if (expired)
            {
                ClearEvidence();
                State = GateState.Grabbed;
            }

            double speed = Length(e.Velocity);
            double up = Dot(e.Velocity, e.Frame.Up);
            double ratio = speed > 0 ? up / speed : 0;
            bool qualifies = up >= Parameters.SustainUpwardSpeed && ratio >= Parameters.MinUpwardRatio;

            if (!qualifies)
            {
                ClearRun();
                if (State == GateState.Armed)
                {
                    if (Parameters.HoldPolicy == ArmedHoldPolicy.HoldForWindow)
                        return Result(true, GateReason.ArmedHeld, null);
                    ClearEvidence();
                    State = GateState.Grabbed;
                    return Result(true, GateReason.Disarmed, null);
                }
                return Result(true, expired ? GateReason.Disarmed : GateReason.EvidenceBuilding, null);
            }

            _run.Add(new Sample { Time = e.Time, UpSpeed = up, Ratio = ratio, Position = e.Position, FrameUp = e.Frame.Up });
            _evidence = Summarize();
            if (ValidEvidence(_evidence) && !_intentIssuedThisGrab)
            {
                bool alreadyArmed = State == GateState.Armed;
                _armingEvidence = _evidence;
                _armingGrabSequence = GrabSequence;
                if (!alreadyArmed) _armedTime = e.Time;
                State = GateState.Armed;
                return Result(true, alreadyArmed ? GateReason.ArmedSustained : GateReason.Armed, null);
            }

            // A partial new run cannot replace or renew the previously validated snapshot.
            return Result(true, State == GateState.Armed ? GateReason.ArmedHeld
                : expired ? GateReason.Disarmed : GateReason.EvidenceBuilding, null);
        }

        // Any rejected sample is treated as lost tracking: evidence is discarded and Armed cannot survive it.
        private GateResult Reject(GateReason reason)
        {
            ClearEvidence();
            if (State == GateState.Armed) State = GateState.Grabbed;
            return Result(false, reason, null);
        }

        private bool ValidEvidence(MotionEvidence e)
        {
            return Finite(e.SustainedSeconds) && Finite(e.MeanUpwardSpeed) && Finite(e.PeakUpwardSpeed)
                && Finite(e.LatestUpwardSpeed) && Finite(e.MinUpwardRatio) && Finite(e.UpwardDisplacement)
                && Finite(e.LastQualifyingTime) && e.SampleCount >= Parameters.MinEvidenceSamples
                && e.SustainedSeconds >= Parameters.MinEvidenceSeconds && e.MeanUpwardSpeed >= Parameters.ArmMeanUpwardSpeed
                && e.UpwardDisplacement >= Parameters.MinUpwardDisplacement && e.MinUpwardRatio >= Parameters.MinUpwardRatio
                && e.LatestUpwardSpeed >= Parameters.SustainUpwardSpeed;
        }

        private bool WithinReleaseWindow(double time)
        {
            double age = time - _armingEvidence.LastQualifyingTime;
            return Finite(age) && age >= 0 && age <= Parameters.ReleaseGraceSeconds
                && (Parameters.HoldPolicy != ArmedHoldPolicy.HoldForWindow || age <= Parameters.ArmedHoldSeconds);
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private MotionEvidence Summarize()
        {
            var first = _run[0]; var last = _run[_run.Count - 1];
            double sum = 0, peak = 0, minRatio = double.MaxValue;
            foreach (var s in _run) { sum += s.UpSpeed; peak = Math.Max(peak, s.UpSpeed); minRatio = Math.Min(minRatio, s.Ratio); }
            double displacement = Dot(last.Position - first.Position, last.FrameUp);
            return new MotionEvidence(_run.Count, last.Time - first.Time, sum / _run.Count, peak, last.UpSpeed, minRatio, displacement, last.Time);
        }

        // Samples must advance the clock strictly. Authority events (grab, release, cancel) may share the
        // timestamp of the sample emitted in the same frame.
        private bool AcceptTime(double t, bool allowEqual)
        {
            if (double.IsNaN(t) || double.IsInfinity(t)) return false;
            if (allowEqual ? t < _lastEventTime : t <= _lastEventTime) return false;
            _lastEventTime = t;
            return true;
        }

        private void ClearRun()
        {
            _run.Clear();
            _evidence = MotionEvidence.Empty;
        }

        private void ClearEvidence()
        {
            ClearRun();
            _armingEvidence = MotionEvidence.Empty;
            _armingGrabSequence = 0;
            _armedTime = double.NaN;
        }

        private GateResult Result(bool accepted, GateReason reason, TossIntent? intent)
        {
            return new GateResult(State, accepted, reason, State == GateState.Armed ? _armingEvidence : _evidence,
                intent, GrabSequence, EventSequence);
        }

        private static double Dot(Vector3 a, Vector3 b) => (double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z;
        private static double Length(Vector3 v) => Math.Sqrt(Dot(v, v));

        private static bool IsUnit(Vector3 v)
        {
            if (!IsFinite(v, 2)) return false;
            return Math.Abs(Length(v) - 1) <= 1e-4;
        }

        private static bool IsFinite(Vector3 v, double maxMagnitude)
        {
            if (float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z)) return false;
            if (float.IsInfinity(v.X) || float.IsInfinity(v.Y) || float.IsInfinity(v.Z)) return false;
            return Length(v) <= maxMagnitude;
        }
    }
}
