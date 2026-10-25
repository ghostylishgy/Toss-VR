// P3 OUTCOME AUTHORITY ISOLATED RESEARCH | TEST TRUST BOUNDARY | NOT INTEGRATED | NOT RUNTIME VALIDATED | NO REVEAL / NO CATCH
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Toss.Experiments.Outcome
{
    public static class OutcomeAuthorityTests
    {
        private static int _cases, _passed, _assertions;
        private static readonly StringBuilder Trace = new StringBuilder();
        private static void Check(bool valid, string why) { _assertions++; if (!valid) throw new Exception(why); }
        private static void Case(string name, Action test)
        {
            _cases++;
            try { test(); _passed++; Console.WriteLine("PASS " + name); }
            catch (Exception e) { Console.WriteLine("FAIL " + name + ": " + e.Message); }
        }

        private sealed class FakeRandomSource : IRandomSource
        {
            private readonly Queue<object> _responses = new Queue<object>();
            public int Calls { get; private set; }
            public readonly List<int> RequestedSizes = new List<int>();
            public FakeRandomSource(params object[] responses) { foreach (var response in responses) _responses.Enqueue(response); }
            public byte[] GetBytes(int count)
            {
                Calls++; RequestedSizes.Add(count);
                if (_responses.Count == 0) return new byte[count];
                var response = _responses.Dequeue();
                if (response is Exception exception) throw exception;
                return (byte[])response;
            }
        }

        private sealed class Driver
        {
            public readonly FakeRandomSource Random;
            public readonly OutcomeAuthority Authority;
            public SessionIdentity Session { get; private set; } = new SessionIdentity(1);
            public Driver(params object[] responses)
            { Random = new FakeRandomSource(responses); Authority = new OutcomeAuthority(Random); Record(Authority.BeginSession(Session)); }
            public OutcomeDecision Open(long toss, long? session = null)
                => Record(Authority.Process(LifecycleEvent.Open(new SessionIdentity(session ?? Session.Value), new TossIdentity(toss))));
            public OutcomeDecision Launch(long toss = 1, long launch = 10, AuthorizationValidity authorization = AuthorizationValidity.Valid,
                long? session = null)
                => Record(Authority.Process(LifecycleEvent.Launch(new SessionIdentity(session ?? Session.Value), new TossIdentity(toss),
                    new LaunchCommitIdentity(launch), authorization)));
            public OutcomeDecision Send(LifecycleEvent e) => Record(Authority.Process(e));
            public OutcomeDecision Consume(long toss = 1, long launch = 10, long? session = null)
                => Record(Authority.Process(LifecycleEvent.Consume(new SessionIdentity(session ?? Session.Value), new TossIdentity(toss),
                    new LaunchCommitIdentity(launch))));
            public OutcomeDecision Begin(long session)
            {
                var decision = Authority.BeginSession(new SessionIdentity(session));
                if (decision.RejectReason == OutcomeRejectReason.None) Session = new SessionIdentity(session);
                return Record(decision);
            }
            private static OutcomeDecision Record(OutcomeDecision d)
            {
                Trace.Append(d.Outcome?.ToString() ?? "None").Append('|').Append(d.CommitStatus).Append('|')
                    .Append(d.OutcomeIdentity?.Session.Value ?? 0).Append('|').Append(d.OutcomeIdentity?.Toss.Value ?? 0).Append('|')
                    .Append(d.OutcomeIdentity?.LaunchCommit.Value ?? 0).Append('|').Append(d.LifecycleStatus).Append('|')
                    .Append(d.RejectReason).Append('|').Append(d.RandomDrawCount).Append('|').Append(d.RandomAttemptCount).AppendLine();
                return d;
            }
        }

        private static void Unchanged(OutcomeDecision before, OutcomeDecision after, string why)
        {
            Check(before.Outcome == after.Outcome && before.OutcomeIdentity.Equals(after.OutcomeIdentity)
                && before.CommitStatus == after.CommitStatus && before.RandomDrawCount == after.RandomDrawCount
                && before.RandomAttemptCount == after.RandomAttemptCount, why);
        }

        public static int Main()
        {
            Console.WriteLine("P3 OUTCOME AUTHORITY | TEST TRUST BOUNDARY | ISOLATED | NOT INTEGRATED | NOT RUNTIME VALIDATED");
            Case("1. Finger Play, SafeAbort and invalid launch never sample", () =>
            {
                var d = new Driver(new byte[] { 0 });
                var finger = d.Send(LifecycleEvent.NonToss(LifecycleEventKind.FingerPlayRelease, d.Session, new TossIdentity(1)));
                Check(finger.CommitStatus == OutcomeCommitStatus.NotApplicable && finger.Outcome == null && d.Random.Calls == 0,
                    "Finger Play has no outcome authority work");
                d.Open(1);
                var abort = d.Send(LifecycleEvent.NonToss(LifecycleEventKind.SafeAbort, d.Session, new TossIdentity(1)));
                Check(abort.CommitStatus == OutcomeCommitStatus.NotApplicable && d.Random.Calls == 0, "SafeAbort does not sample");
                d.Open(2);
                Check(d.Launch(2, authorization: AuthorizationValidity.Invalid).RejectReason == OutcomeRejectReason.UnauthorizedLaunch
                    && d.Random.Calls == 0, "invalid authorization cannot sample");
                Check(d.Launch(2, launch: 0).RejectReason == OutcomeRejectReason.InvalidLaunchCommit && d.Random.Calls == 0,
                    "invalid commit identity cannot sample");
                Check(d.Launch(2).Outcome == CoinOutcome.Heads && d.Random.Calls == 1, "only a later valid trusted commit may sample");
            });
            Case("2. Legal launch samples once and duplicate launch is read-only", () =>
            {
                var d = new Driver(new byte[] { 1 }); d.Open(1);
                var first = d.Launch();
                Check(first.Outcome == CoinOutcome.Tails && first.CommitStatus == OutcomeCommitStatus.Committed
                    && first.RandomAttemptCount == 1 && first.RandomDrawCount == 1 && d.Random.RequestedSizes[0] == 1,
                    "one valid commit requests exactly one byte and commits once");
                var duplicate = d.Launch();
                Check(duplicate.RejectReason == OutcomeRejectReason.DuplicateLaunch && d.Random.Calls == 1, "duplicate cannot redraw");
                Unchanged(first, duplicate, "duplicate returns identical outcome and identity");
            });
            Case("3. Committed result is immutable under flight and illegal transitions", () =>
            {
                var d = new Driver(new byte[] { 0 }); d.Open(1); var committed = d.Launch();
                var flight = d.Send(LifecycleEvent.Flight(d.Session, new TossIdentity(1), new LaunchCommitIdentity(10),
                    new FlightTelemetry(double.NaN, double.PositiveInfinity, -999, 1e300, 500)));
                Check(flight.RejectReason == OutcomeRejectReason.None, "presentation telemetry is accepted only as a diagnostic event");
                Unchanged(committed, flight, "NaN pose, extreme rotation and collisions cannot affect outcome");
                var cancel = d.Send(LifecycleEvent.NonToss(LifecycleEventKind.Cancel, d.Session, new TossIdentity(1)));
                Check(cancel.RejectReason == OutcomeRejectReason.IllegalTransition && cancel.Outcome == null,
                    "post-commit Cancel rejection is outcome-free");
                Unchanged(committed, d.Launch(), "rejected Cancel does not mutate the retained committed result");
                Check(d.Open(1).RejectReason == OutcomeRejectReason.StaleToss && d.Random.Calls == 1, "same toss cannot reopen");
            });
            Case("4. Reset preserves committed history but revokes pending authorization", () =>
            {
                var committed = new Driver(new byte[] { 0 }); committed.Open(1); var before = committed.Launch();
                var reset = committed.Send(LifecycleEvent.Reset(committed.Session));
                Unchanged(before, reset, "Reset cannot change a valid committed toss");
                var replay = committed.Launch();
                Check(replay.RejectReason == OutcomeRejectReason.DuplicateLaunch && committed.Random.Calls == 1,
                    "Reset plus old commit replay remains deduplicated");
                var pending = new Driver(); pending.Open(1); pending.Send(LifecycleEvent.Reset(pending.Session));
                Check(pending.Launch().RejectReason == OutcomeRejectReason.StaleToss && pending.Random.Calls == 0,
                    "Reset revokes an uncommitted launch rather than reauthorizing it");
                Check(pending.Open(1).RejectReason == OutcomeRejectReason.StaleToss, "Reset does not recycle toss identity");
                pending.Open(2); pending.Launch(2);
                Check(pending.Random.Calls == 1, "only a fresh toss can draw after reset");
            });
            Case("5. Session end, monotonic replacement and old-session replay are isolated", () =>
            {
                var d = new Driver(new byte[] { 0 }, new byte[] { 1 }); d.Open(1); d.Launch();
                d.Send(LifecycleEvent.End(d.Session));
                Check(d.Launch().RejectReason == OutcomeRejectReason.SessionEnded && d.Random.Calls == 1,
                    "ended session cannot launch or redraw");
                Check(d.Begin(2).RejectReason == OutcomeRejectReason.RevealPending && d.Session.Value == 1,
                    "Session end does not bypass unconsumed outcome retention");
                d.Consume();
                d.Begin(2); d.Open(1); var second = d.Launch();
                Check(second.Outcome == CoinOutcome.Tails && second.OutcomeIdentity.Value.Session.Value == 2 && d.Random.Calls == 2,
                    "same local toss ID in a new session is a distinct outcome");
                Check(d.Launch(session: 1).RejectReason == OutcomeRejectReason.SessionMismatch && d.Random.Calls == 2,
                    "old session replay cannot mutate current state");
                Check(d.Begin(2).RejectReason == OutcomeRejectReason.SessionMismatch, "session identity cannot be reused");
                d.Consume();
                d.Begin(3);
                Check(d.Launch(session: 2).RejectReason == OutcomeRejectReason.SessionMismatch && d.Random.Calls == 2,
                    "expired session history is rejected, not regenerated");
            });
            Case("6. Storage is bounded and expired toss history fails closed", () =>
            {
                var d = new Driver(new byte[] { 0 }, new byte[] { 1 }); d.Open(1); d.Launch(); d.Consume(); d.Open(2); d.Launch(2, 20);
                var replay = d.Launch(1, 10);
                Check(replay.RejectReason == OutcomeRejectReason.StaleToss && replay.Outcome == null && d.Random.Calls == 2,
                    "evicted history cannot be recreated or returned as current");
                foreach (var field in typeof(OutcomeAuthority).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                    Check(!typeof(ICollection).IsAssignableFrom(field.FieldType), "authority must not accumulate collections: " + field.Name);
            });
            Case("7. RNG failure is terminal for that toss and never auto-retried", () =>
            {
                var d = new Driver(new CryptographicException("synthetic"), new byte[] { 0 }); d.Open(1);
                var failed = d.Launch();
                Check(failed.RejectReason == OutcomeRejectReason.RandomSourceFailed && failed.CommitStatus == OutcomeCommitStatus.Failed
                    && failed.Outcome == null && failed.RandomAttemptCount == 1 && failed.RandomDrawCount == 0, "RNG exception fails closed");
                var duplicate = d.Launch();
                Check(duplicate.RejectReason == OutcomeRejectReason.DuplicateLaunch && d.Random.Calls == 1, "failed attempt is not retried");
                d.Send(LifecycleEvent.Reset(d.Session));
                Check(d.Launch().RejectReason == OutcomeRejectReason.StaleToss && d.Random.Calls == 1, "Reset cannot retry failed old launch");
                d.Open(2); d.Launch(2);
                Check(d.Random.Calls == 2 && d.Random.RequestedSizes.TrueForAll(size => size == 1), "only a new toss gets a new attempt");
            });
            Case("8. Null and wrong-length random data are invalid and terminal", () =>
            {
                foreach (var bytes in new[] { null, new byte[0], new byte[2] })
                {
                    var d = new Driver(bytes); d.Open(1); var failed = d.Launch();
                    Check(failed.RejectReason == OutcomeRejectReason.InvalidRandomData && failed.Outcome == null
                        && failed.RandomAttemptCount == 1 && failed.RandomDrawCount == 0, "malformed RNG response fails closed");
                    Check(d.Launch().RejectReason == OutcomeRejectReason.DuplicateLaunch && d.Random.Calls == 1,
                        "malformed response cannot trigger a redraw");
                }
            });
            Case("9. Binary mapping is exact and production RNG API returns requested bytes", () =>
            {
                foreach (var pair in new[] { new[] { (byte)0, (byte)CoinOutcome.Heads }, new[] { (byte)1, (byte)CoinOutcome.Tails },
                    new[] { (byte)254, (byte)CoinOutcome.Heads }, new[] { (byte)255, (byte)CoinOutcome.Tails } })
                {
                    var d = new Driver(new[] { pair[0] }); d.Open(1); var result = d.Launch();
                    Check(result.Outcome == (CoinOutcome)pair[1] && d.Random.Calls == 1 && d.Random.RequestedSizes[0] == 1,
                        "low-bit mapping for byte " + pair[0]);
                }
                using (var rng = new CryptographicRandomSource())
                    Check(rng.GetBytes(1).Length == 1, "RandomNumberGenerator.GetBytes compatibility smoke test");
            });
            Case("10. Identity mismatches cannot replace a commit or borrow another session", () =>
            {
                var d = new Driver(new byte[] { 0 }); d.Open(1); var committed = d.Launch();
                var mismatch = d.Launch(launch: 11);
                Check(mismatch.RejectReason == OutcomeRejectReason.LaunchMismatch && d.Random.Calls == 1, "launch identity cannot be replaced");
                Check(mismatch.Outcome == null, "rejected mismatch carries no outcome");
                Unchanged(committed, d.Launch(), "mismatch does not change the retained result");
                d.Consume();
                d.Open(2);
                Check(d.Launch(3, 30).RejectReason == OutcomeRejectReason.NoPendingToss && d.Random.Calls == 1,
                    "unknown future toss cannot sample");
                Check(d.Launch(2, 20, session: 99).RejectReason == OutcomeRejectReason.SessionMismatch && d.Random.Calls == 1,
                    "foreign session cannot borrow current pending toss");
                Check(d.Open(0).RejectReason == OutcomeRejectReason.InvalidToss, "nonpositive toss identity rejected");
            });
            Case("11. Commit-boundary ordering is deterministic and has no fallback path", () =>
            {
                var cancelled = new Driver(); cancelled.Open(1);
                cancelled.Send(LifecycleEvent.NonToss(LifecycleEventKind.Cancel, cancelled.Session, new TossIdentity(1)));
                Check(cancelled.Launch().RejectReason == OutcomeRejectReason.StaleToss && cancelled.Random.Calls == 0,
                    "Cancel before commit wins; later commit is stale");
                var noCommit = new Driver(); noCommit.Open(1);
                var abort = noCommit.Send(LifecycleEvent.NonToss(LifecycleEventKind.SafeAbort, noCommit.Session, new TossIdentity(1)));
                Check(abort.Outcome == null && abort.CommitStatus == OutcomeCommitStatus.NotApplicable && noCommit.Random.Calls == 0,
                    "missing trusted commit models failed flight initialization without outcome or fallback");
                var committed = new Driver(new byte[] { 1 }); committed.Open(1); var before = committed.Launch();
                var after = committed.Send(LifecycleEvent.NonToss(LifecycleEventKind.Cancel, committed.Session, new TossIdentity(1)));
                Check(after.RejectReason == OutcomeRejectReason.IllegalTransition && after.Outcome == null,
                    "post-commit Cancel rejection has no outcome payload");
                Unchanged(before, committed.Launch(), "commit before Cancel remains authoritative and readable");
            });
            Case("12. Illegal state transitions are side-effect free", () =>
            {
                var d = new Driver(new byte[] { 0 }); d.Open(1);
                var earlyFlight = d.Send(LifecycleEvent.Flight(d.Session, new TossIdentity(1), new LaunchCommitIdentity(10), default));
                Check(earlyFlight.RejectReason == OutcomeRejectReason.IllegalTransition && d.Random.Calls == 0,
                    "flight presentation cannot precede outcome commit");
                Check(d.Open(2).RejectReason == OutcomeRejectReason.TossBusy && d.Random.Calls == 0,
                    "a second pending toss cannot displace the first");
                var unknown = d.Send(LifecycleEvent.NonToss((LifecycleEventKind)999, d.Session, new TossIdentity(1)));
                Check(unknown.RejectReason == OutcomeRejectReason.IllegalTransition && d.Random.Calls == 0,
                    "unknown default lifecycle event rejected");
                d.Launch(); d.Send(LifecycleEvent.End(d.Session));
                Check(d.Send(LifecycleEvent.Reset(d.Session)).RejectReason == OutcomeRejectReason.SessionEnded && d.Random.Calls == 1,
                    "ended session rejects Reset without reopening history");
            });
            Case("13. Unconsumed outcome blocks a new Toss without changing the retained result", () =>
            {
                var d = new Driver(new byte[] { 0 }); d.Open(1); var committed = d.Launch();
                var rejected = d.Open(2);
                Check(rejected.RejectReason == OutcomeRejectReason.RevealPending && rejected.Outcome == null && d.Random.Calls == 1,
                    "new Toss rejection must not expose A or draw again");
                var reread = d.Launch();
                Unchanged(committed, reread, "rejected OpenToss must leave A readable and immutable");
            });
            Case("14. Unconsumed outcome blocks silent Session replacement", () =>
            {
                var d = new Driver(new byte[] { 0 }); d.Open(1); var committed = d.Launch();
                var rejected = d.Begin(2);
                Check(rejected.RejectReason == OutcomeRejectReason.RevealPending && rejected.Outcome == null && d.Random.Calls == 1,
                    "new Session rejection must not expose A or draw again");
                var reread = d.Launch(session: 1);
                Unchanged(committed, reread, "rejected BeginSession must preserve the original Session and outcome");
            });
            Case("15. Invalid identities reject with no retained outcome attached", () =>
            {
                var d = new Driver(new byte[] { 0 }); d.Open(1); var committed = d.Launch();
                Check(d.Open(0).Outcome == null, "invalid Toss Open must not return A");
                Check(d.Launch(launch: 0).Outcome == null, "invalid Launch identity must not return A");
                Check(d.Begin(0).Outcome == null, "invalid Session identity must not return A");
                Unchanged(committed, d.Launch(), "invalid events must not mutate A");
                Check(d.Random.Calls == 1, "invalid events cannot draw");
            });
            Case("16. Wrong Launch rejection is outcome-free while legal replay remains readable", () =>
            {
                var d = new Driver(new byte[] { 0 }); d.Open(1); var committed = d.Launch();
                var wrong = d.Launch(launch: 11);
                Check(wrong.RejectReason == OutcomeRejectReason.LaunchMismatch && wrong.Outcome == null
                    && wrong.OutcomeIdentity == null, "wrong Launch must not carry A outcome or identity");
                Unchanged(committed, d.Launch(), "correct idempotent Launch still returns A");
                Check(d.Random.Calls == 1, "wrong and duplicate Launch cannot draw again");
            });
            Case("17. RevealConsumed requires exact ownership and releases bounded storage once", () =>
            {
                var d = new Driver(new byte[] { 0 }, new byte[] { 1 }); d.Open(1); var committed = d.Launch();
                var wrongToss = d.Launch(toss: 2, launch: 20);
                Check(wrongToss.RejectReason == OutcomeRejectReason.NoPendingToss && wrongToss.Outcome == null
                    && wrongToss.OutcomeIdentity == null, "wrong Toss request cannot carry A result");
                var wrongConsumeToss = d.Consume(toss: 2);
                Check(wrongConsumeToss.RejectReason == OutcomeRejectReason.TossMismatch && wrongConsumeToss.Outcome == null,
                    "another Toss cannot consume or read A");
                var wrongConsumeLaunch = d.Consume(launch: 11);
                Check(wrongConsumeLaunch.RejectReason == OutcomeRejectReason.LaunchMismatch && wrongConsumeLaunch.Outcome == null,
                    "wrong Launch cannot consume or read A");
                var foreignConsume = d.Consume(session: 99);
                Check(foreignConsume.RejectReason == OutcomeRejectReason.SessionMismatch && foreignConsume.Outcome == null,
                    "foreign Session cannot consume or read A");
                Check(d.Consume(toss: 0).Outcome == null && d.Consume(launch: 0).Outcome == null,
                    "invalid consumption identities carry no result");
                Unchanged(committed, d.Launch(), "invalid consumption attempts leave A retained");
                var reset = d.Send(LifecycleEvent.Reset(d.Session));
                Unchanged(committed, reset, "Reset cannot bypass or alter unconsumed retention");
                var consumed = d.Consume();
                Check(consumed.RejectReason == OutcomeRejectReason.None && consumed.Outcome == null
                    && consumed.LifecycleStatus == OutcomeLifecycleStatus.AwaitingToss && d.Random.Calls == 1,
                    "matching TEST TRUST BOUNDARY acknowledgement releases storage without drawing");
                Check(d.Consume().RejectReason == OutcomeRejectReason.StaleToss && d.Consume().Outcome == null,
                    "duplicate consumption is an outcome-free stale replay");
                Check(d.Launch().RejectReason == OutcomeRejectReason.StaleToss && d.Launch().Outcome == null && d.Random.Calls == 1,
                    "evicted A cannot sample again");
                d.Open(2); var second = d.Launch(2, 20);
                Check(second.Outcome == CoinOutcome.Tails && d.Random.Calls == 2, "only post-consumption B can draw");
                d.Consume(2, 20); d.Begin(2);
                Check(d.Launch(2, 20, session: 1).RejectReason == OutcomeRejectReason.SessionMismatch && d.Random.Calls == 2,
                    "normal Session switch leaves only monotonic identity barriers, not retained outcomes");
            });
            using (var sha = SHA256.Create())
                Console.WriteLine("TRACE SHA256 " + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Trace.ToString()))).Replace("-", ""));
            Console.WriteLine("OFFLINE RESULT: " + _passed + "/" + _cases + " groups, assertions=" + _assertions);
            Console.WriteLine("SYNTHETIC TRUSTED COMMITS ONLY | O(1) ACTIVE-SESSION HISTORY | NO PERSISTENCE | NO REVEAL / NO CATCH");
            return _passed == _cases ? 0 : 1;
        }
    }
}
