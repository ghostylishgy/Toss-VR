// P3 OUTCOME AUTHORITY ISOLATED RESEARCH | TEST TRUST BOUNDARY | NOT INTEGRATED | NOT RUNTIME VALIDATED | NO REVEAL / NO CATCH
// Pure .NET outcome lifecycle candidate. LaunchCommitted events are simulated trusted commits, not production Handoff authorization.
// Calls are externally serialized: this type intentionally has no cross-thread or cross-module transaction claim.
using System;
using System.Security.Cryptography;

namespace Toss.Experiments.Outcome
{
    public enum CoinOutcome { Heads, Tails }
    public enum OutcomeCommitStatus { None, AwaitingLaunch, Committed, Failed, NotApplicable }
    public enum OutcomeLifecycleStatus { NoSession, AwaitingToss, AwaitingLaunch, Committed, Failed, SessionEnded }
    public enum AuthorizationValidity { Invalid, Valid }
    public enum LifecycleEventKind { OpenToss, LaunchCommitted, FingerPlayRelease, SafeAbort, Cancel, Reset, EndSession, FlightObserved, RevealConsumed }
    public enum OutcomeRejectReason
    {
        None, InvalidSession, SessionMismatch, SessionEnded, InvalidToss, StaleToss, TossBusy, NoPendingToss,
        TossMismatch, InvalidLaunchCommit, LaunchMismatch, UnauthorizedLaunch, DuplicateLaunch, RandomSourceFailed,
        InvalidRandomData, IllegalTransition, RevealPending
    }

    public readonly struct SessionIdentity
    {
        public long Value { get; }
        public bool IsValid => Value > 0;
        public SessionIdentity(long value) { Value = value; }
    }

    public readonly struct TossIdentity
    {
        public long Value { get; }
        public bool IsValid => Value > 0;
        public TossIdentity(long value) { Value = value; }
    }

    public readonly struct LaunchCommitIdentity
    {
        public long Value { get; }
        public bool IsValid => Value > 0;
        public LaunchCommitIdentity(long value) { Value = value; }
    }

    public readonly struct OutcomeIdentity : IEquatable<OutcomeIdentity>
    {
        public SessionIdentity Session { get; }
        public TossIdentity Toss { get; }
        public LaunchCommitIdentity LaunchCommit { get; }
        public OutcomeIdentity(SessionIdentity session, TossIdentity toss, LaunchCommitIdentity launchCommit)
        { Session = session; Toss = toss; LaunchCommit = launchCommit; }
        public bool Equals(OutcomeIdentity other) => Session.Value == other.Session.Value && Toss.Value == other.Toss.Value
            && LaunchCommit.Value == other.LaunchCommit.Value;
        public override bool Equals(object obj) => obj is OutcomeIdentity other && Equals(other);
        public override int GetHashCode() => unchecked(((Session.Value.GetHashCode() * 397) ^ Toss.Value.GetHashCode()) * 397
            ^ LaunchCommit.Value.GetHashCode());
    }

    // Diagnostic-only presentation input. OutcomeAuthority never derives or changes a result from these values.
    public readonly struct FlightTelemetry
    {
        public double PositionX { get; }
        public double PositionY { get; }
        public double PositionZ { get; }
        public double AngularSpeed { get; }
        public long CollisionCount { get; }
        public FlightTelemetry(double x, double y, double z, double angularSpeed, long collisionCount)
        { PositionX = x; PositionY = y; PositionZ = z; AngularSpeed = angularSpeed; CollisionCount = collisionCount; }
    }

    // TEST TRUST BOUNDARY: public construction exists so isolated tests can simulate an already-committed launch.
    // A production adapter must instead prove a successful, source-authenticated Handoff launch; these values alone do not do so.
    public readonly struct LifecycleEvent
    {
        public LifecycleEventKind Kind { get; }
        public SessionIdentity Session { get; }
        public TossIdentity Toss { get; }
        public LaunchCommitIdentity LaunchCommit { get; }
        public AuthorizationValidity Authorization { get; }
        public FlightTelemetry Telemetry { get; }

        private LifecycleEvent(LifecycleEventKind kind, SessionIdentity session, TossIdentity toss,
            LaunchCommitIdentity launchCommit, AuthorizationValidity authorization, FlightTelemetry telemetry)
        { Kind = kind; Session = session; Toss = toss; LaunchCommit = launchCommit; Authorization = authorization; Telemetry = telemetry; }

        public static LifecycleEvent Open(SessionIdentity session, TossIdentity toss)
            => new LifecycleEvent(LifecycleEventKind.OpenToss, session, toss, default, AuthorizationValidity.Invalid, default);
        public static LifecycleEvent Launch(SessionIdentity session, TossIdentity toss, LaunchCommitIdentity launch,
            AuthorizationValidity authorization)
            => new LifecycleEvent(LifecycleEventKind.LaunchCommitted, session, toss, launch, authorization, default);
        public static LifecycleEvent NonToss(LifecycleEventKind kind, SessionIdentity session, TossIdentity toss)
            => new LifecycleEvent(kind, session, toss, default, AuthorizationValidity.Invalid, default);
        public static LifecycleEvent Reset(SessionIdentity session)
            => new LifecycleEvent(LifecycleEventKind.Reset, session, default, default, AuthorizationValidity.Invalid, default);
        public static LifecycleEvent End(SessionIdentity session)
            => new LifecycleEvent(LifecycleEventKind.EndSession, session, default, default, AuthorizationValidity.Invalid, default);
        public static LifecycleEvent Flight(SessionIdentity session, TossIdentity toss, LaunchCommitIdentity launch,
            FlightTelemetry telemetry)
            => new LifecycleEvent(LifecycleEventKind.FlightObserved, session, toss, launch, AuthorizationValidity.Invalid, telemetry);
        // TEST TRUST BOUNDARY: simulates a future RevealConsumer acknowledgement; no Unity or real visual Reveal exists here.
        public static LifecycleEvent Consume(SessionIdentity session, TossIdentity toss, LaunchCommitIdentity launch)
            => new LifecycleEvent(LifecycleEventKind.RevealConsumed, session, toss, launch, AuthorizationValidity.Invalid, default);
    }

    public readonly struct OutcomeDecision
    {
        public CoinOutcome? Outcome { get; }
        public OutcomeCommitStatus CommitStatus { get; }
        public OutcomeIdentity? OutcomeIdentity { get; }
        public OutcomeLifecycleStatus LifecycleStatus { get; }
        public OutcomeRejectReason RejectReason { get; }
        public int RandomDrawCount { get; }
        public int RandomAttemptCount { get; }
        public string Diagnostics { get; }
        internal OutcomeDecision(CoinOutcome? outcome, OutcomeCommitStatus status, OutcomeIdentity? identity,
            OutcomeLifecycleStatus lifecycle, OutcomeRejectReason reason, int draws, int attempts, string diagnostics)
        { Outcome = outcome; CommitStatus = status; OutcomeIdentity = identity; LifecycleStatus = lifecycle;
            RejectReason = reason; RandomDrawCount = draws; RandomAttemptCount = attempts; Diagnostics = diagnostics; }
    }

    public interface IRandomSource
    {
        // Must return exactly count cryptographically random bytes, or throw. No timestamp or presentation input is involved.
        byte[] GetBytes(int count);
    }

    public sealed class CryptographicRandomSource : IRandomSource, IDisposable
    {
        private readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
        public byte[] GetBytes(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var buffer = new byte[count];
            _rng.GetBytes(buffer);
            return buffer;
        }
        public void Dispose() { _rng.Dispose(); }
    }

    public sealed class OutcomeAuthority
    {
        private sealed class Record
        {
            public TossIdentity Toss;
            public LaunchCommitIdentity Launch;
            public bool HasLaunch;
            public OutcomeCommitStatus Status;
            public CoinOutcome? Outcome;
        }

        private readonly IRandomSource _random;
        private SessionIdentity _session;
        private OutcomeLifecycleStatus _lifecycle = OutcomeLifecycleStatus.NoSession;
        private Record _record;
        private long _lastToss;
        private int _randomDrawCount;
        private int _randomAttemptCount;

        public OutcomeAuthority(IRandomSource randomSource)
        { _random = randomSource ?? throw new ArgumentNullException(nameof(randomSource)); }

        public OutcomeDecision BeginSession(SessionIdentity session)
        {
            if (!session.IsValid) return Decision(OutcomeRejectReason.InvalidSession, "Session identity must be positive.");
            if (_lifecycle != OutcomeLifecycleStatus.NoSession && session.Value <= _session.Value)
                return Decision(OutcomeRejectReason.SessionMismatch, "Session identities must strictly increase.");
            if (_record != null && _record.Status == OutcomeCommitStatus.Committed)
                return Decision(OutcomeRejectReason.RevealPending, "A committed outcome cannot be evicted before matching RevealConsumed.");
            _session = session;
            _record = null;
            _lastToss = 0;
            _lifecycle = OutcomeLifecycleStatus.AwaitingToss;
            return Decision(OutcomeRejectReason.None, "New session; previous in-memory history is no longer accepted.");
        }

        public OutcomeDecision Process(LifecycleEvent e)
        {
            if (e.Session.Value != _session.Value || _lifecycle == OutcomeLifecycleStatus.NoSession)
                return Decision(OutcomeRejectReason.SessionMismatch, "Event does not belong to the active session.", includeRetainedOutcome: false);
            if (_lifecycle == OutcomeLifecycleStatus.SessionEnded && e.Kind != LifecycleEventKind.RevealConsumed)
                return Decision(OutcomeRejectReason.SessionEnded, "Ended sessions reject everything except matching RevealConsumed.");
            switch (e.Kind)
            {
                case LifecycleEventKind.OpenToss: return OpenToss(e);
                case LifecycleEventKind.LaunchCommitted: return CommitLaunch(e);
                case LifecycleEventKind.FingerPlayRelease:
                case LifecycleEventKind.SafeAbort:
                case LifecycleEventKind.Cancel: return Abort(e);
                case LifecycleEventKind.Reset: return Reset();
                case LifecycleEventKind.EndSession:
                    _lifecycle = OutcomeLifecycleStatus.SessionEnded;
                    return Decision(OutcomeRejectReason.None, "Session ended; retained bytes are inert and expire on the next session.");
                case LifecycleEventKind.FlightObserved: return ObserveFlight(e);
                case LifecycleEventKind.RevealConsumed: return ConsumeReveal(e);
                default: return Decision(OutcomeRejectReason.IllegalTransition, "Unknown lifecycle event.");
            }
        }

        private OutcomeDecision OpenToss(LifecycleEvent e)
        {
            if (!e.Toss.IsValid) return Decision(OutcomeRejectReason.InvalidToss, "Toss identity must be positive.");
            if (e.Toss.Value <= _lastToss) return Decision(OutcomeRejectReason.StaleToss, "Toss identities cannot be reused within a session.");
            if (_record != null && _record.Status == OutcomeCommitStatus.Committed)
                return Decision(OutcomeRejectReason.RevealPending, "A new toss cannot evict an unconsumed committed outcome.");
            if (_record != null && _record.Status == OutcomeCommitStatus.AwaitingLaunch)
                return Decision(OutcomeRejectReason.TossBusy, "The current uncommitted toss must resolve before another opens.");
            _lastToss = e.Toss.Value;
            _record = new Record { Toss = e.Toss, Status = OutcomeCommitStatus.AwaitingLaunch };
            _lifecycle = OutcomeLifecycleStatus.AwaitingLaunch;
            return Decision(OutcomeRejectReason.None, "Toss opened without sampling.");
        }

        private OutcomeDecision CommitLaunch(LifecycleEvent e)
        {
            if (!e.Toss.IsValid) return Decision(OutcomeRejectReason.InvalidToss, "Toss identity must be positive.");
            if (!e.LaunchCommit.IsValid) return Decision(OutcomeRejectReason.InvalidLaunchCommit, "Launch commit identity must be positive.");
            if (_record == null || _record.Toss.Value != e.Toss.Value)
                return Decision(e.Toss.Value <= _lastToss ? OutcomeRejectReason.StaleToss : OutcomeRejectReason.NoPendingToss,
                    "No matching live toss; replay cannot recreate expired history.", includeRetainedOutcome: false);
            if (_record.HasLaunch)
            {
                if (_record.Launch.Value != e.LaunchCommit.Value)
                    return Decision(OutcomeRejectReason.LaunchMismatch, "One toss cannot replace its launch commit identity.");
                return Decision(OutcomeRejectReason.DuplicateLaunch, "Duplicate launch is read-only; no RNG retry or new draw.", includeRetainedOutcome: true);
            }
            if (_record.Status != OutcomeCommitStatus.AwaitingLaunch)
                return Decision(OutcomeRejectReason.IllegalTransition, "Launch is illegal in the current outcome state.");
            if (e.Authorization != AuthorizationValidity.Valid)
                return Decision(OutcomeRejectReason.UnauthorizedLaunch, "Untrusted or invalid launch cannot sample.");

            // Poison the attempt before entering user-supplied RNG: failure is terminal for this toss, never a retry loop.
            _record.HasLaunch = true;
            _record.Launch = e.LaunchCommit;
            _randomAttemptCount++;
            byte[] bytes;
            try { bytes = _random.GetBytes(1); }
            catch (Exception) { return Fail(OutcomeRejectReason.RandomSourceFailed, "Random source failed; outcome remains uncommitted."); }
            if (bytes == null || bytes.Length != 1)
                return Fail(OutcomeRejectReason.InvalidRandomData, "Random source must return exactly one byte.");

            _randomDrawCount++;
            // 256 equally likely byte values map to 128 Heads + 128 Tails; no modulo reduction or rejection redraw is needed.
            _record.Outcome = (bytes[0] & 1) == 0 ? CoinOutcome.Heads : CoinOutcome.Tails;
            _record.Status = OutcomeCommitStatus.Committed;
            _lifecycle = OutcomeLifecycleStatus.Committed;
            return Decision(OutcomeRejectReason.None, "Outcome committed once; presentation may only converge to it.", includeRetainedOutcome: true);
        }

        private OutcomeDecision Fail(OutcomeRejectReason reason, string diagnostic)
        {
            _record.Status = OutcomeCommitStatus.Failed;
            _record.Outcome = null;
            _lifecycle = OutcomeLifecycleStatus.Failed;
            return Decision(reason, diagnostic);
        }

        private OutcomeDecision Abort(LifecycleEvent e)
        {
            if (_record != null && _record.Toss.Value == e.Toss.Value)
            {
                if (_record.Status == OutcomeCommitStatus.Committed || _record.Status == OutcomeCommitStatus.Failed)
                    return Decision(OutcomeRejectReason.IllegalTransition, "Terminal outcome history cannot be replaced by an abort event.");
                _record = null;
                _lifecycle = OutcomeLifecycleStatus.AwaitingToss;
            }
            return new OutcomeDecision(null, OutcomeCommitStatus.NotApplicable, null, _lifecycle,
                OutcomeRejectReason.None, _randomDrawCount, _randomAttemptCount,
                e.Kind + " selected no outcome and caused no sampling.");
        }

        private OutcomeDecision Reset()
        {
            if (_record != null && _record.Status == OutcomeCommitStatus.AwaitingLaunch)
            {
                _record = null;
                _lifecycle = OutcomeLifecycleStatus.AwaitingToss;
                return Decision(OutcomeRejectReason.None, "Reset invalidated the uncommitted toss; its identity remains below the replay barrier.");
            }
            if (_record != null && _record.Status == OutcomeCommitStatus.Failed)
            {
                _record = null;
                _lifecycle = OutcomeLifecycleStatus.AwaitingToss;
                return Decision(OutcomeRejectReason.None, "Reset cleared failed transient state without reopening the old toss identity.");
            }
            return Decision(OutcomeRejectReason.None, "Reset retained the committed outcome and its identity unchanged.", includeRetainedOutcome: true);
        }

        private OutcomeDecision ConsumeReveal(LifecycleEvent e)
        {
            if (!e.Toss.IsValid) return Decision(OutcomeRejectReason.InvalidToss, "RevealConsumed requires a positive toss identity.");
            if (!e.LaunchCommit.IsValid) return Decision(OutcomeRejectReason.InvalidLaunchCommit, "RevealConsumed requires the committed launch identity.");
            if (_record == null) return Decision(OutcomeRejectReason.StaleToss, "Consumed or expired history cannot be consumed again.");
            if (_record.Toss.Value != e.Toss.Value)
                return Decision(OutcomeRejectReason.TossMismatch, "Another toss cannot consume the retained outcome.");
            if (_record.Status != OutcomeCommitStatus.Committed)
                return Decision(OutcomeRejectReason.IllegalTransition, "Only a committed outcome can be RevealConsumed.");
            if (_record.Launch.Value != e.LaunchCommit.Value)
                return Decision(OutcomeRejectReason.LaunchMismatch, "RevealConsumed must match the complete outcome identity.");

            // Evict only after a matching simulated consumer acknowledgement; the per-session high-water mark remains.
            _record = null;
            if (_lifecycle != OutcomeLifecycleStatus.SessionEnded) _lifecycle = OutcomeLifecycleStatus.AwaitingToss;
            return Decision(OutcomeRejectReason.None, "TEST TRUST BOUNDARY RevealConsumed accepted; result storage released.");
        }

        private OutcomeDecision ObserveFlight(LifecycleEvent e)
        {
            if (_record == null || _record.Toss.Value != e.Toss.Value)
                return Decision(OutcomeRejectReason.StaleToss, "Flight observation has no retained matching toss.", includeRetainedOutcome: false);
            if (_record.Status != OutcomeCommitStatus.Committed)
                return Decision(OutcomeRejectReason.IllegalTransition, "Flight observation cannot create an outcome.");
            if (_record.Launch.Value != e.LaunchCommit.Value)
                return Decision(OutcomeRejectReason.LaunchMismatch, "Flight observation identity does not match the commit.");
            // Telemetry is deliberately unread: trajectory, rotation, speed and collisions have no outcome authority.
            return Decision(OutcomeRejectReason.None, "Presentation observation ignored for outcome purposes; committed value retained.", includeRetainedOutcome: true);
        }

        // Rejections are outcome-free by default. Only an explicitly matching legal read/commit may opt in.
        private OutcomeDecision Decision(OutcomeRejectReason reason, string diagnostic, bool includeRetainedOutcome = false)
        {
            bool committed = includeRetainedOutcome && _record != null && _record.Status == OutcomeCommitStatus.Committed;
            return new OutcomeDecision(committed ? _record.Outcome : null,
                _record != null ? _record.Status : OutcomeCommitStatus.None,
                committed ? new OutcomeIdentity(_session, _record.Toss, _record.Launch) : (OutcomeIdentity?)null,
                _lifecycle, reason, _randomDrawCount, _randomAttemptCount, diagnostic);
        }
    }
}
