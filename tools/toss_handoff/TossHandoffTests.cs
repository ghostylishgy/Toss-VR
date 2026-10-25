using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Toss.Experiments.Flight;
using Toss.Experiments.TossIntent;

namespace Toss.Experiments.Handoff
{
    public static class TossHandoffTests
    {
        private static int _cases, _passed, _assertions;
        private static readonly StringBuilder Trace = new StringBuilder();
        private static void Check(bool valid, string why)
        {
            _assertions++;
            if (!valid) throw new Exception(why);
        }
        private static void Case(string name, Action test)
        {
            _cases++;
            try { test(); _passed++; Console.WriteLine("PASS " + name); }
            catch (Exception e) { Console.WriteLine("FAIL " + name + ": " + e.Message); }
        }
        private static ReleaseContext Context(Vector3? position = null, Quaternion? orientation = null,
            Vector3? velocity = null, Vector3? angular = null, Vector3? gravity = null, MotionFrame? frame = null,
            bool owner = true, bool cancelled = false, bool poseValid = true, bool motionValid = true)
            => new ReleaseContext(position ?? new Vector3(0, 1, 0.5f), orientation ?? Quaternion.Identity,
                velocity ?? new Vector3(0, 3, 0), angular ?? new Vector3(2, 0, 0), gravity ?? new Vector3(0, -8, 0),
                frame ?? MotionFrame.WorldYUp, owner, cancelled, poseValid, motionValid);

        private sealed class Driver
        {
            public readonly TossHandoff H = new TossHandoff(100, TossIntentParameters.Experimental(),
                FlightLimits.Experimental(), MotionFrame.WorldYUp);
            public long Sequence;
            public double Time;
            private readonly Dictionary<string, ReleasePath> _actions = new Dictionary<string, ReleasePath>();
            public Observation Send(GateEvent e, int? grab = null)
            {
                int identity = grab ?? (e.Kind == GateEventKind.GrabBegin || e.Kind == GateEventKind.Reset ? 0 : H.GrabSequence);
                var o = H.Observe(H.Session, ++Sequence, identity, e);
                Trace.Append(H.Session).Append('|').Append(Sequence).Append('|').Append(identity).Append('|')
                    .Append(e.Kind).Append('|').Append(o.Accepted).Append('|').Append(o.Reason).Append('|')
                    .Append(o.Gate?.Reason).Append('|').Append(o.Gate?.EventSequence).AppendLine();
                return o;
            }
            public void Grab()
            {
                Time += 0.01;
                Check(Send(GateEvent.GrabBegin(Time)).Accepted, "synthetic GrabBegin accepted");
            }
            public void Move(int count = 10)
            {
                for (int i = 0; i < count; i++)
                {
                    Time += 1.0 / 72;
                    Check(Send(GateEvent.Sample(Time, new Vector3(0, (float)Time, 0.5f), Vector3.UnitY,
                        MotionFrame.WorldYUp)).Accepted, "synthetic upward sample accepted");
                }
            }
            public ReleaseReceipt Release() => Send(GateEvent.Release(Time)).Receipt;
            public ReleaseReceipt Toss()
            {
                Grab(); Move();
                var receipt = Release();
                Check(receipt != null && receipt.Gate.TossIntentAccepted, "receipt comes from a real accepted gate event chain");
                return receipt;
            }
            public Arbitration Resolve(ReleaseReceipt receipt, ReleaseContext context)
            {
                var result = H.ResolveRelease(receipt, context);
                Trace.Append(receipt?.Session).Append('|').Append(receipt?.SourceSequence).Append('|')
                    .Append(receipt?.Gate.GrabSequence).Append('|').Append(result.Path).Append('|').Append(result.Reason)
                    .Append('|').Append(result.IntentAccepted).Append('|').Append(result.LaunchConsumed).AppendLine();
                var state = H.CurrentFlight;
                foreach (double x in new[] { state.Elapsed, state.Position.X, state.Position.Y, state.Position.Z,
                    state.Orientation.X, state.Orientation.Y, state.Orientation.Z, state.Orientation.W,
                    state.LinearVelocity.X, state.LinearVelocity.Y, state.LinearVelocity.Z,
                    state.AngularVelocity.X, state.AngularVelocity.Y, state.AngularVelocity.Z })
                {
                    if (double.IsNaN(x) || double.IsInfinity(x)) throw new Exception("non-finite flight state");
                    Trace.Append(x.ToString("R", CultureInfo.InvariantCulture)).Append('|');
                }
                Trace.Append(state.Intent.Session).Append('|').Append(state.Intent.Sequence).Append('|')
                    .Append(state.Status).Append('|').Append(H.LaunchAttempts).Append('|').Append(H.Launches).AppendLine();
                if (result.Path != ReleasePath.SafeAbort)
                {
                    string key = receipt.Session + ":" + receipt.SourceSequence;
                    Check(!_actions.ContainsKey(key), "a Release may select exactly one action only once");
                    _actions.Add(key, result.Path);
                }
                if (result.Path == ReleasePath.TossFlightLaunch)
                {
                    var intent = receipt.Gate.Intent.Value;
                    var e = intent.Evidence;
                    Check(result.IntentAccepted && result.LaunchConsumed && result.Flight.Value.RequestAccepted,
                        "accepted intent and consumed launch are both explicit");
                    Check(intent.IntentId == intent.GrabSequence && intent.GrabSequence == receipt.Gate.GrabSequence
                        && state.Intent.Session == receipt.Session && state.Intent.Sequence == intent.IntentId,
                        "session, grab, intent and launch identities agree");
                    Check(receipt.Gate.EventAccepted && receipt.Gate.Reason == GateReason.TossIntentAccepted
                        && receipt.Gate.EventSequence > 0 && receipt.SourceSequence > 0, "accepted Release is traceable to gate event");
                    Check(e.SampleCount >= 3 && e.SustainedSeconds >= 0.06 && e.MeanUpwardSpeed >= 0.6
                        && e.UpwardDisplacement >= 0.04 && e.MinUpwardRatio >= 0.5, "launch has complete gate evidence");
                    foreach (double x in new[] { e.SustainedSeconds, e.MeanUpwardSpeed, e.PeakUpwardSpeed, e.LatestUpwardSpeed,
                        e.MinUpwardRatio, e.UpwardDisplacement, e.LastQualifyingTime, intent.ArmedTime, intent.ReleaseTime })
                        if (double.IsNaN(x) || double.IsInfinity(x)) throw new Exception("non-finite intent evidence");
                }
                return result;
            }
        }

        public static int Main()
        {
            Case("1. Ordinary Finger Play selects no Flight", () =>
            {
                var d = new Driver(); d.Grab();
                var r = d.Release();
                var a = d.Resolve(r, Context());
                Check(a.Path == ReleasePath.FingerPlayRelease && !a.IntentAccepted && !a.LaunchConsumed, "ordinary release only selects Finger Play");
                Check(d.H.LaunchAttempts == 0 && d.H.CurrentFlight.Status == FlightStatus.Idle, "no Flight call for Finger Play");
                Check(d.Resolve(r, Context()).Reason == HandoffReason.ReleaseAlreadyResolved, "Finger Play cannot be selected twice either");
                var late = new Driver(); late.Grab(); late.Move(); late.Time += 0.11;
                var lr = late.Release();
                Check(lr.Gate.Reason == GateReason.ReleaseTooLate && late.Resolve(lr, Context()).Path == ReleasePath.FingerPlayRelease,
                    "expired Toss qualification does not become a launch");
            });
            Case("2. A real accepted Intent and valid explicit launch start once", () =>
            {
                var d = new Driver(); var receipt = d.Toss();
                Check(d.H.Launches == 0 && d.H.GateState == GateState.Flight, "logical Intent acceptance is not Flight consumption");
                Check(d.Resolve(receipt, Context()).Path == ReleasePath.TossFlightLaunch, "one launch selected");
                Check(d.H.Launches == 1 && d.H.LaunchAttempts == 1 && d.H.CurrentFlight.Status == FlightStatus.Running, "one numerical Flight starts");
                Check(d.H.CurrentFlight.LinearVelocity == new Vector3(0, 3, 0)
                    && d.H.CurrentFlight.AngularVelocity == new Vector3(2, 0, 0), "explicit velocities are preserved, not Finger Play scaled or clamped");
            });
            Case("3. Duplicate source Release and redelivery with a new sequence cannot relaunch", () =>
            {
                var d = new Driver(); var r = d.Toss(); d.Resolve(r, Context());
                var duplicate = d.H.Observe(d.H.Session, r.SourceSequence, r.Gate.GrabSequence, GateEvent.Release(d.Time));
                Check(!duplicate.Accepted && duplicate.Reason == HandoffReason.DuplicateRelease && duplicate.Receipt == null,
                    "same source event rejected before the gate");
                var redelivered = d.Send(GateEvent.Release(d.Time));
                Check(!redelivered.Accepted && redelivered.Reason == HandoffReason.DuplicateRelease && redelivered.Receipt == null,
                    "new transport sequence cannot bypass the gate one-intent-per-grab rule");
                Check(d.H.Launches == 1 && d.H.LaunchAttempts == 1, "neither duplicate calls Flight");
            });
            Case("4. Already-consumed Intent, cloned result and positive identity alone are not authority", () =>
            {
                var d = new Driver(); var r = d.Toss();
                var cloned = new ReleaseReceipt(d.H, r.Session, r.SourceSequence, r.Gate, r.Frame);
                Check(d.Resolve(cloned, Context()).Reason == HandoffReason.UntrustedReceipt, "even a full copied GateResult is not an issued receipt");
                var forged = new GateResult(GateState.Flight, true, GateReason.TossIntentAccepted, MotionEvidence.Empty,
                    new Toss.Experiments.TossIntent.TossIntent(1, 1, 0, 0, MotionEvidence.Empty), 1, 1);
                Check(d.Resolve(new ReleaseReceipt(d.H, 100, 1, forged, MotionFrame.WorldYUp), Context()).Path == ReleasePath.SafeAbort,
                    "positive identity with forged accepted flag and empty evidence is rejected");
                Check(d.Resolve(null, Context()).Reason == HandoffReason.UntrustedReceipt && d.H.LaunchAttempts == 0, "missing proof never starts Flight");
                d.Resolve(r, Context());
                var repeated = d.Resolve(r, Context());
                Check(repeated.Path == ReleasePath.SafeAbort && repeated.Reason == HandoffReason.IntentAlreadyConsumed
                    && repeated.IntentAccepted && repeated.LaunchConsumed && d.H.LaunchAttempts == 1, "accepted and already-consumed are distinct diagnostics");
            });
            Case("5. Cancel before Release or between acceptance and launch aborts", () =>
            {
                var before = new Driver(); before.Grab(); before.Move();
                Check(before.Send(GateEvent.Cancel(before.Time)).Accepted, "Cancel handled by gate");
                Check(before.Release() == null && before.H.Launches == 0, "cancelled grab has no release receipt");
                var after = new Driver(); var r = after.Toss();
                Check(after.Send(GateEvent.Cancel(after.Time)).Accepted, "pending transaction cancelled even after gate's logical Flight transition");
                Check(after.Resolve(r, Context()).Reason == HandoffReason.Cancelled && after.H.LaunchAttempts == 0, "cancel revokes pending receipt");
                var flag = new Driver(); var fr = flag.Toss();
                Check(flag.Resolve(fr, Context(cancelled: true)).Reason == HandoffReason.Cancelled && flag.H.Launches == 0, "explicit cancellation flag aborts");
            });
            Case("6. Ownership loss is terminal for this grab's handoff", () =>
            {
                foreach (bool toss in new[] { false, true })
                {
                    var d = new Driver(); d.Grab(); if (toss) d.Move(); var r = d.Release();
                    Check(d.Resolve(r, Context(owner: false)).Reason == HandoffReason.OwnershipLost, "invalid owner selects neither release action");
                    Check(d.Resolve(r, Context()).Path == ReleasePath.SafeAbort && d.H.LaunchAttempts == 0, "corrected flags cannot retry resolved event");
                }
                var lost = new Driver(); lost.Grab(); lost.Move(); lost.Time += 1.0 / 72;
                Check(!lost.Send(GateEvent.Sample(lost.Time, Vector3.Zero, Vector3.UnitY, MotionFrame.WorldYUp, false)).Accepted,
                    "gate rejects ownership-invalid sample");
                lost.Move();
                Check(lost.Resolve(lost.Release(), Context()).Reason == HandoffReason.OwnershipLost && lost.H.Launches == 0,
                    "later samples cannot silently reacquire handoff authority in the same grab");
            });
            Case("7. Valid Intent cannot bypass launch validation or fall back after failure", () =>
            {
                foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                foreach (var context in new[] { Context(position: new Vector3(bad, 1, 0.5f)), Context(velocity: new Vector3(0, bad, 0)),
                    Context(angular: new Vector3(bad, 0, 0)), Context(gravity: new Vector3(0, bad, 0)), Context(orientation: new Quaternion(0, 0, 0, bad)) })
                {
                    var d = new Driver(); var r = d.Toss(); var a = d.Resolve(r, context);
                    Check(a.Path == ReleasePath.SafeAbort && a.Reason == HandoffReason.InvalidLaunch && a.IntentAccepted
                        && !a.LaunchConsumed && d.H.Launches == 0, "non-finite launch aborts despite legal Intent");
                    Check(d.Resolve(r, Context()).Reason == HandoffReason.ReleaseAlreadyResolved, "failed event cannot fall back or retry");
                }
                foreach (var context in new[] { Context(velocity: new Vector3(0, 7, 0)), Context(orientation: new Quaternion()),
                    Context(position: new Vector3(10, 1, 0.5f)) })
                {
                    var d = new Driver(); var r = d.Toss(); var a = d.Resolve(r, context);
                    Check(a.Path == ReleasePath.SafeAbort && a.Flight.Value.RequestReason == RequestReason.InvalidLaunch
                        && d.H.LaunchAttempts == 1 && d.H.Launches == 0, "Flight retains numeric bounds and quaternion validation authority");
                    Check(d.Resolve(r, Context()).Reason == HandoffReason.ReleaseAlreadyResolved && d.H.LaunchAttempts == 1,
                        "Flight initialization failure cannot execute Finger Play or retry the launch");
                }
            });
            Case("8. Single-path arbitration also rejects inconsistent identity, frame and validity", () =>
            {
                var identity = new Driver(); identity.Grab(); identity.Move();
                var wrong = identity.Send(GateEvent.Release(identity.Time), identity.H.GrabSequence + 1);
                Check(!wrong.Accepted && wrong.Reason == HandoffReason.GrabMismatch && wrong.Receipt == null, "wrong grab cannot release current grab");
                identity.Resolve(identity.Release(), Context());
                foreach (var context in new[] { Context(poseValid: false), Context(motionValid: false),
                    Context(frame: new MotionFrame(1, Vector3.UnitY)), Context(frame: new MotionFrame(0, Vector3.UnitZ)) })
                {
                    var d = new Driver(); var r = d.Toss();
                    Check(d.Resolve(r, context).Path == ReleasePath.SafeAbort && d.H.LaunchAttempts == 0, "invalid flags or inconsistent frame select no action");
                    Check(d.Resolve(r, Context()).Path == ReleasePath.SafeAbort, "one event never emits the other action later");
                }
                Check(default(Arbitration).Path == ReleasePath.SafeAbort, "default output is non-actionable");
                Check(typeof(ReleaseReceipt).GetConstructors().Length == 0, "public callers cannot construct release receipts");
                var staleActions = new List<string>();
                foreach (bool toss in new[] { false, true })
                {
                    var delayed = new Driver(); delayed.Grab(); if (toss) delayed.Move(); var r = delayed.Release();
                    delayed.Time += 1.0 / 72;
                    Check(!delayed.Send(GateEvent.Sample(delayed.Time, new Vector3(0, 1, 0.5f), Vector3.UnitY,
                        MotionFrame.WorldYUp)).Accepted, "post-release motion is rejected by the gate");
                    var stale = delayed.Resolve(r, Context());
                    if (stale.Path != ReleasePath.SafeAbort || stale.Reason != HandoffReason.Superseded || delayed.H.LaunchAttempts != 0)
                        staleActions.Add(stale.Path.ToString());
                }
                Check(staleActions.Count == 0, "newer same-grab observation must revoke old authority; stale actions=" + string.Join(",", staleActions));
                var replay = new Driver(); var pending = replay.Toss();
                Check(!replay.H.Observe(replay.H.Session, pending.SourceSequence, pending.Gate.GrabSequence,
                    GateEvent.Release(replay.Time)).Accepted, "duplicate source Release rejected without superseding original");
                Check(!replay.H.Observe(99, 999, pending.Gate.GrabSequence, GateEvent.Release(replay.Time)).Accepted,
                    "foreign-session event cannot supersede current receipt");
                Check(replay.Resolve(pending, Context()).Path == ReleasePath.TossFlightLaunch, "original pending receipt survives ignored replay and foreign events");
            });
            Case("9. Reset never reopens consumed or pending old Release events", () =>
            {
                var d = new Driver(); var old = d.Toss(); d.Resolve(old, Context()); d.Send(GateEvent.Reset());
                Check(d.H.CurrentFlight.Status == FlightStatus.Idle, "Reset clears numerical motion");
                Check(d.Resolve(old, Context()).Reason == HandoffReason.IntentAlreadyConsumed, "old consumed receipt remains consumed after Reset");
                Check(d.H.Observe(d.H.Session, old.SourceSequence, old.Gate.GrabSequence, GateEvent.Release(d.Time)).Reason
                    == HandoffReason.DuplicateRelease, "Reset preserves transport replay barrier");
                var fresh = d.Toss();
                Check(fresh.Gate.GrabSequence > old.Gate.GrabSequence, "same gate Reset never reuses grab identity");
                d.Resolve(fresh, Context());
                Check(d.H.Launches == 2, "only the fresh grab starts another flight");
                var pending = new Driver(); var pr = pending.Toss(); pending.Send(GateEvent.Reset());
                Check(pending.Resolve(pr, Context()).Reason == HandoffReason.Reset && pending.H.Launches == 0, "Reset also invalidates unconsumed acceptance");
            });
            Case("10. Synthetic sessions namespace IDs and reject old or foreign receipts", () =>
            {
                var d = new Driver(); var old = d.Toss(); d.Resolve(old, Context());
                d.H.BeginSession(101); d.Sequence = 0; d.Time = 0;
                Check(d.Resolve(old, Context()).Reason == HandoffReason.WrongSession, "old session receipt rejected");
                Check(d.H.Observe(100, 999, old.Gate.GrabSequence, GateEvent.Release(1)).Reason == HandoffReason.WrongSession,
                    "late event from old session cannot enter new gate");
                var fresh = d.Toss(); d.Resolve(fresh, Context());
                Check(fresh.Gate.Intent.Value.IntentId == old.Gate.Intent.Value.IntentId && d.H.CurrentFlight.Intent.Session == 101
                    && d.H.Launches == 2, "same local intent ID in new explicit session is unambiguous");
                bool reuseRejected = false;
                try { d.H.BeginSession(100); } catch (ArgumentOutOfRangeException) { reuseRejected = true; }
                Check(reuseRejected, "synthetic session keys must increase, not be reused");
                var foreign = new Driver(); var foreignReceipt = foreign.Toss();
                var local = new Driver(); var localReceipt = local.Toss();
                Check(local.Resolve(foreignReceipt, Context()).Reason == HandoffReason.UntrustedReceipt && local.H.Launches == 0,
                    "another gate instance with identical numeric IDs cannot impersonate this issuer");
                local.Resolve(localReceipt, Context());
            });
            using (var sha = SHA256.Create())
                Console.WriteLine("TRACE SHA256 " + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Trace.ToString()))).Replace("-", ""));
            Console.WriteLine("OFFLINE RESULT: " + _passed + "/" + _cases + " groups, assertions=" + _assertions);
            Console.WriteLine("SYNTHETIC EVENT CHAIN ONLY | P2 HANDOFF ISOLATED | NOT INTEGRATED | NOT RUNTIME VALIDATED | NO RNG / NO RESULT / NO CATCH");
            return _passed == _cases ? 0 : 1;
        }
    }
}
