using System;
using System.Numerics;
using Toss.Experiments.Flight;
using Toss.Experiments.TossIntent;

namespace Toss.Experiments.Handoff
{
    public enum ReleasePath { SafeAbort, FingerPlayRelease, TossFlightLaunch }
    public enum HandoffReason
    {
        None, Observed, WrongSession, StaleEvent, DuplicateRelease, GrabMismatch, GateRejected,
        Cancelled, OwnershipLost, FrameMismatch, Reset, SessionChanged, Superseded,
        UntrustedReceipt, ReleaseAlreadyResolved, IntentAlreadyConsumed, InvalidFlags,
        InvalidLaunch, FlightUnavailable, FingerPlaySelected, FlightStarted
    }

    public sealed class ReleaseReceipt
    {
        public long Session { get; }
        public long SourceSequence { get; }
        public GateResult Gate { get; }
        public MotionFrame Frame { get; }
        internal TossHandoff Issuer { get; }
        internal bool Resolved, LaunchConsumed;
        internal HandoffReason Invalidated;

        internal ReleaseReceipt(TossHandoff issuer, long session, long sequence, GateResult gate, MotionFrame frame)
        {
            Issuer = issuer; Session = session; SourceSequence = sequence; Gate = gate; Frame = frame;
        }
    }

    public readonly struct Observation
    {
        public bool Accepted { get; }
        public HandoffReason Reason { get; }
        public GateResult? Gate { get; }
        public ReleaseReceipt Receipt { get; }
        internal Observation(bool accepted, HandoffReason reason, GateResult? gate = null, ReleaseReceipt receipt = null)
        {
            Accepted = accepted; Reason = reason; Gate = gate; Receipt = receipt;
        }
    }

    public readonly struct ReleaseContext
    {
        public Vector3 Position { get; }
        public Quaternion Orientation { get; }
        public Vector3 Velocity { get; }
        public Vector3 AngularVelocity { get; }
        public Vector3 Gravity { get; }
        public MotionFrame Frame { get; }
        public bool OwnershipValid { get; }
        public bool Cancelled { get; }
        public bool PoseValid { get; }
        public bool MotionValid { get; }

        public ReleaseContext(Vector3 position, Quaternion orientation, Vector3 velocity, Vector3 angularVelocity,
            Vector3 gravity, MotionFrame frame, bool ownershipValid, bool cancelled, bool poseValid, bool motionValid)
        {
            Position = position; Orientation = orientation; Velocity = velocity; AngularVelocity = angularVelocity;
            Gravity = gravity; Frame = frame; OwnershipValid = ownershipValid; Cancelled = cancelled;
            PoseValid = poseValid; MotionValid = motionValid;
        }
    }

    public readonly struct Arbitration
    {
        public ReleasePath Path { get; }
        public HandoffReason Reason { get; }
        public bool IntentAccepted { get; }
        public bool LaunchConsumed { get; }
        public FlightState? Flight { get; }
        internal Arbitration(ReleasePath path, HandoffReason reason, ReleaseReceipt receipt, FlightState? flight = null)
        {
            Path = path; Reason = reason; IntentAccepted = receipt != null && receipt.Gate.TossIntentAccepted;
            LaunchConsumed = receipt != null && receipt.LaunchConsumed; Flight = flight;
        }
    }

    public sealed class TossHandoff
    {
        private readonly TossIntentParameters _parameters;
        private readonly MotionFrame _frame;
        private readonly FlightDynamics _flight;
        private TossIntentGate _gate;
        private ReleaseReceipt _pending;
        private long _lastSourceSequence;
        private HandoffReason _grabAbort;
        public long Session { get; private set; }
        public GateState GateState => _gate.State;
        public int GrabSequence => _gate.GrabSequence;
        public FlightState CurrentFlight => _flight.Current;
        public int LaunchAttempts { get; private set; }
        public int Launches { get; private set; }

        public TossHandoff(long syntheticSession, TossIntentParameters parameters, FlightLimits limits, MotionFrame frame)
        {
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            if (frame.FrameId != parameters.ExpectedFrameId || !Finite(frame.Up)
                || Math.Abs(Math.Sqrt((double)frame.Up.X * frame.Up.X + (double)frame.Up.Y * frame.Up.Y
                    + (double)frame.Up.Z * frame.Up.Z) - 1) > 1e-4)
                throw new ArgumentException("A fixed finite unit motion frame is required.", nameof(frame));
            _frame = frame;
            _flight = new FlightDynamics(limits);
            BeginSession(syntheticSession);
        }

        public void BeginSession(long syntheticSession)
        {
            if (syntheticSession <= Session) throw new ArgumentOutOfRangeException(nameof(syntheticSession));
            InvalidatePending(HandoffReason.SessionChanged);
            Session = syntheticSession;
            _lastSourceSequence = 0;
            _grabAbort = HandoffReason.None;
            _gate = new TossIntentGate(_parameters);
            _flight.Reset();
        }

        public Observation Observe(long session, long sourceSequence, int grabSequence, GateEvent input)
        {
            if (session != Session) return new Observation(false, HandoffReason.WrongSession);
            if (sourceSequence <= _lastSourceSequence)
                return new Observation(false, input.Kind == GateEventKind.Release ? HandoffReason.DuplicateRelease : HandoffReason.StaleEvent);
            _lastSourceSequence = sourceSequence;
            bool independent = input.Kind == GateEventKind.GrabBegin || input.Kind == GateEventKind.Reset;
            if (independent ? grabSequence != 0 : grabSequence <= 0 || grabSequence != _gate.GrabSequence)
                return new Observation(false, HandoffReason.GrabMismatch);

            if (input.Kind == GateEventKind.Reset)
            {
                InvalidatePending(HandoffReason.Reset);
                _grabAbort = HandoffReason.None;
                _flight.Reset();
                return new Observation(true, HandoffReason.Reset, _gate.Process(input));
            }
            if (input.Kind == GateEventKind.Cancel)
            {
                bool pending = _pending != null;
                InvalidatePending(HandoffReason.Cancelled);
                _grabAbort = HandoffReason.Cancelled;
                var cancelled = _gate.Process(input);
                return new Observation(pending || cancelled.EventAccepted, HandoffReason.Cancelled, cancelled);
            }
            if (input.Kind == GateEventKind.MotionSample)
            {
                if (!input.OwnershipValid) _grabAbort = HandoffReason.OwnershipLost;
                else if (!SameFrame(input.Frame)) _grabAbort = HandoffReason.FrameMismatch;
                if (_grabAbort != HandoffReason.None) InvalidatePending(_grabAbort);
            }

            // A later admitted event supersedes unresolved release authority even if the gate rejects that event.
            InvalidatePending(HandoffReason.Superseded);
            var result = _gate.Process(input);
            if (!result.EventAccepted)
                return new Observation(false, result.Reason == GateReason.DuplicateRelease
                    ? HandoffReason.DuplicateRelease : HandoffReason.GateRejected, result);
            if (input.Kind == GateEventKind.GrabBegin) _grabAbort = HandoffReason.None;
            if (input.Kind != GateEventKind.Release) return new Observation(true, HandoffReason.Observed, result);
            _pending = new ReleaseReceipt(this, Session, sourceSequence, result, _frame);
            _pending.Invalidated = _grabAbort;
            return new Observation(true, HandoffReason.Observed, result, _pending);
        }

        public Arbitration ResolveRelease(ReleaseReceipt receipt, ReleaseContext context)
        {
            if (receipt == null || !ReferenceEquals(receipt.Issuer, this))
                return Abort(HandoffReason.UntrustedReceipt, null);
            if (receipt.Session != Session) return Abort(HandoffReason.WrongSession, receipt);
            if (receipt.LaunchConsumed) return Abort(HandoffReason.IntentAlreadyConsumed, receipt);
            if (receipt.Resolved) return Abort(HandoffReason.ReleaseAlreadyResolved, receipt);
            if (receipt.Invalidated != HandoffReason.None) return Abort(receipt.Invalidated, receipt);
            if (!ReferenceEquals(receipt, _pending)) return Abort(HandoffReason.UntrustedReceipt, receipt);

            // Resolution is single-use even when validation fails; it never falls through to another release path.
            receipt.Resolved = true;
            _pending = null;
            if (context.Cancelled) return Abort(HandoffReason.Cancelled, receipt);
            if (!context.OwnershipValid) return Abort(HandoffReason.OwnershipLost, receipt);
            if (!context.PoseValid || !context.MotionValid) return Abort(HandoffReason.InvalidFlags, receipt);
            if (!SameFrame(context.Frame)) return Abort(HandoffReason.FrameMismatch, receipt);
            if (!Finite(context.Position) || !Finite(context.Velocity) || !Finite(context.AngularVelocity)
                || !Finite(context.Gravity) || !Finite(context.Orientation.X) || !Finite(context.Orientation.Y)
                || !Finite(context.Orientation.Z) || !Finite(context.Orientation.W))
                return Abort(HandoffReason.InvalidLaunch, receipt);

            var result = receipt.Gate;
            if (!result.TossIntentAccepted)
                return result.Reason == GateReason.FingerPlayRelease || result.Reason == GateReason.ReleaseTooLate
                    ? new Arbitration(ReleasePath.FingerPlayRelease, HandoffReason.FingerPlaySelected, receipt)
                    : Abort(HandoffReason.GateRejected, receipt);

            // Only this private gate issues receipts; its validated evidence is authoritative, not a caller-built GateResult.
            var intent = result.Intent.Value;
            var launch = new LaunchInput(new IntentIdentity(Session, intent.IntentId), context.Position, context.Orientation,
                context.Velocity, context.AngularVelocity, context.Gravity);
            LaunchAttempts++;
            var flight = _flight.Launch(launch);
            if (!flight.RequestAccepted)
                return new Arbitration(ReleasePath.SafeAbort, flight.RequestReason == RequestReason.InvalidLaunch
                    ? HandoffReason.InvalidLaunch : HandoffReason.FlightUnavailable, receipt, flight);
            receipt.LaunchConsumed = true;
            Launches++;
            return new Arbitration(ReleasePath.TossFlightLaunch, HandoffReason.FlightStarted, receipt, flight);
        }

        private void InvalidatePending(HandoffReason reason)
        {
            if (_pending != null) _pending.Invalidated = reason;
            _pending = null;
        }

        private bool SameFrame(MotionFrame frame) => frame.FrameId == _frame.FrameId && frame.Up == _frame.Up;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.X) && Finite(value.Y) && Finite(value.Z);
        private static Arbitration Abort(HandoffReason reason, ReleaseReceipt receipt)
            => new Arbitration(ReleasePath.SafeAbort, reason, receipt);
    }
}
