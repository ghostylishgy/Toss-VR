// P2 TOSS INTENT ISOLATED PROTOTYPE | NOT INTEGRATED | NOT RUNTIME VALIDATED | NO RNG / NO PHYSICS FLIGHT
// Each case states why the gate accepts or rejects. They prove rules on synthetic deterministic input,
// not that real hands behave this way.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Toss.Experiments.TossIntent
{
    public static class TossIntentTests
    {
        private static readonly List<string> Failures = new List<string>();
        private static int _cases, _passed, _assertions;

        private static void Check(bool ok, string what) { _assertions++; if (!ok) Failures.Add(what); }

        private static void Case(string name, Action body)
        {
            _cases++;
            int before = Failures.Count;
            try { body(); }
            catch (Exception e) { Failures.Add(name + ": unexpected " + e.GetType().Name + ": " + e.Message); }
            if (Failures.Count == before) { _passed++; Console.WriteLine("PASS " + name); }
            else
            {
                Console.WriteLine("FAIL " + name);
                for (int i = before; i < Failures.Count; i++) Console.WriteLine("    " + Failures[i]);
            }
        }

        // Drives the gate with a synthetic hand: constant-velocity segments sampled at a fixed rate.
        private sealed class Driver
        {
            public readonly TossIntentGate Gate;
            public readonly List<GateResult> Log = new List<GateResult>();
            public double Time;
            public Vector3 Position = new Vector3(0f, 1.0f, 0.35f);
            public MotionFrame Frame = MotionFrame.WorldYUp;
            public bool Ownership = true;
            public double ArmedAt = double.NaN;
            public int ArmedSampleCount = -1;

            public Driver(TossIntentParameters p = null) { Gate = new TossIntentGate(p ?? TossIntentParameters.Experimental()); }

            public GateResult Push(GateEvent e)
            {
                var r = Gate.Process(e);
                Log.Add(r);
                if (r.TossIntentAccepted)
                    Check(CompleteEvidence(r.Intent.Value.Evidence, Gate.Parameters)
                        && r.Intent.Value.GrabSequence == r.GrabSequence, "accepted intent must carry complete finite evidence from this grab");
                if (r.Reason == GateReason.Armed && double.IsNaN(ArmedAt)) { ArmedAt = e.Time; ArmedSampleCount = r.Evidence.SampleCount; }
                return r;
            }

            public GateResult Grab() => Push(GateEvent.GrabBegin(Time));
            public GateResult Release() => Push(GateEvent.Release(Time));
            public GateResult Cancel() => Push(GateEvent.Cancel(Time));
            public GateResult Reset() => Push(GateEvent.Reset());

            public GateResult Move(double seconds, Vector3 velocity, double rate = 72, bool movePosition = true)
            {
                double dt = 1.0 / rate;
                int n = (int)Math.Round(seconds * rate);
                GateResult last = default;
                for (int i = 0; i < n; i++)
                {
                    Time += dt;
                    if (movePosition) Position += velocity * (float)dt;
                    last = Push(GateEvent.Sample(Time, Position, velocity, Frame, Ownership));
                }
                return last;
            }

            public GateResult Still(double seconds, double rate = 72) => Move(seconds, Vector3.Zero, rate);

            public void Jitter(double seconds, float amplitudeSpeed, double rate = 72)
            {
                double dt = 1.0 / rate;
                int n = (int)Math.Round(seconds * rate);
                for (int i = 0; i < n; i++)
                {
                    var v = new Vector3(0f, (i % 2 == 0) ? amplitudeSpeed : -amplitudeSpeed, 0f);
                    Time += dt;
                    Position += v * (float)dt;
                    Push(GateEvent.Sample(Time, Position, v, Frame, Ownership));
                }
            }

            public bool EverArmed => Log.Exists(r => r.State == GateState.Armed);
            public int Intents => Gate.IntentsAccepted;
        }

        private static readonly Vector3 Up1 = new Vector3(0f, 1.0f, 0f);

        private static bool CompleteEvidence(MotionEvidence e, TossIntentParameters p)
        {
            foreach (double v in new[] { e.SustainedSeconds, e.MeanUpwardSpeed, e.PeakUpwardSpeed,
                e.LatestUpwardSpeed, e.MinUpwardRatio, e.UpwardDisplacement, e.LastQualifyingTime })
                if (double.IsNaN(v) || double.IsInfinity(v)) return false;
            return e.SampleCount >= p.MinEvidenceSamples && e.SustainedSeconds >= p.MinEvidenceSeconds
                && e.MeanUpwardSpeed >= p.ArmMeanUpwardSpeed && e.UpwardDisplacement >= p.MinUpwardDisplacement
                && e.MinUpwardRatio >= p.MinUpwardRatio;
        }

        private static Driver HeldDriver(double holdSeconds = 0.15)
        {
            var d = new Driver(TossIntentParameters.Experimental(ArmedHoldPolicy.HoldForWindow, holdSeconds));
            d.Grab(); d.Move(0.12, Up1);
            return d;
        }

        public static int Main()
        {
            Console.WriteLine("P2 TOSS INTENT GATE | OFFLINE | NOT INTEGRATED | NOT RUNTIME VALIDATED | NO RNG / NO PHYSICS FLIGHT");

            Case("1. Ordinary finger play never produces a toss", () =>
            {
                var still = new Driver(); still.Grab(); still.Still(0.5);
                var r = still.Release();
                Check(!still.EverArmed && r.Reason == GateReason.FingerPlayRelease && still.Intents == 0, "held still: FingerPlayRelease, no intent");

                var wiggle = new Driver(); wiggle.Grab();
                wiggle.Move(0.3, new Vector3(0.1f, 0.05f, 0f)); wiggle.Move(0.3, new Vector3(-0.1f, -0.05f, 0f));
                r = wiggle.Release();
                Check(!wiggle.EverArmed && r.Reason == GateReason.FingerPlayRelease, "gentle wiggle (0.05 m/s up): below sustain floor, never Armed");

                var jitter = new Driver(); jitter.Grab(); jitter.Jitter(0.5, 0.9f);
                r = jitter.Release();
                Check(!jitter.EverArmed && jitter.Intents == 0, "alternating +-0.9 m/s jitter: run resets every other frame, never sustained");

                var slow = new Driver(); slow.Grab(); slow.Move(1.0, new Vector3(0f, 0.3f, 0f));
                r = slow.Release();
                Check(!slow.EverArmed && r.Reason == GateReason.FingerPlayRelease, "slow lift 0.3 m/s for 1 s: sustained but mean < arm speed, never Armed");
                var ev = slow.Log[slow.Log.Count - 2].Evidence;
                Check(ev.SampleCount > 60 && ev.MeanUpwardSpeed < 0.6 && ev.UpwardDisplacement > 0.25, "slow lift evidence recorded but insufficient (mean " + ev.MeanUpwardSpeed.ToString("F2") + ")");
            });

            Case("2. Legal upward motion reaches Armed on accumulated evidence, not a single frame", () =>
            {
                var d = new Driver(); d.Grab();
                var r = d.Move(0.12, Up1);
                Check(r.State == GateState.Armed, "1.0 m/s up for 0.12 s -> Armed");
                Check(d.ArmedSampleCount >= 3, "armed with " + d.ArmedSampleCount + " samples (>= MinEvidenceSamples)");
                Check(d.Log[1].Reason == GateReason.EvidenceBuilding && d.Log[2].Reason == GateReason.EvidenceBuilding, "first upward frames only build evidence");
                var ev = d.Log[d.Log.Count - 1].Evidence;
                Check(ev.SustainedSeconds >= 0.06 && ev.MeanUpwardSpeed >= 0.6 && ev.UpwardDisplacement >= 0.04 && ev.MinUpwardRatio >= 0.5,
                    "evidence: sustained " + ev.SustainedSeconds.ToString("F3") + " s, mean " + ev.MeanUpwardSpeed.ToString("F2") + ", disp " + ev.UpwardDisplacement.ToString("F3"));
                Check(d.Intents == 0, "Armed alone is not an intent");

                var one = new Driver(); one.Grab(); one.Still(0.2);
                var spike = one.Move(1.0 / 72, new Vector3(0f, 3f, 0f));
                Check(spike.State == GateState.Grabbed && spike.Reason == GateReason.EvidenceBuilding, "single 3 m/s frame is evidence, not Armed");
            });

            Case("3. Armed + legal Release produces exactly one TossIntent and a logical Flight", () =>
            {
                var d = new Driver(); d.Grab(); d.Move(0.12, Up1);
                var r = d.Release();
                Check(r.TossIntentAccepted && r.Reason == GateReason.TossIntentAccepted && r.State == GateState.Flight, "Release in Armed -> TossIntentAccepted, Flight");
                Check(r.Intent.HasValue && r.Intent.Value.IntentId == d.Gate.GrabSequence && r.Intent.Value.ReleaseTime == d.Time
                    && r.Intent.Value.ArmedTime == d.ArmedAt, "intent carries grab id, armed time and release time");
                Check(r.Intent.Value.Evidence.SampleCount > 0 && r.Evidence.SampleCount > 0, "motion evidence attached to the intent");
                Check(d.Intents == 1, "exactly one intent");
                var reset = d.Reset();
                Check(reset.State == GateState.Ready && reset.Evidence.SampleCount == 0, "Reset returns Flight -> Ready with empty evidence");
            });

            Case("4. Duplicate Release is not a second toss", () =>
            {
                var d = new Driver(); d.Grab(); d.Move(0.12, Up1); d.Release();
                d.Time += 0.01;
                var dup = d.Release();
                Check(!dup.EventAccepted && !dup.TossIntentAccepted && dup.Reason == GateReason.DuplicateRelease && dup.State == GateState.Flight,
                    "second Release in Flight rejected as DuplicateRelease");
                Check(d.Intents == 1, "still one intent");
                d.Reset(); d.Time += 0.01;
                var idle = d.Release();
                Check(!idle.EventAccepted && idle.Reason == GateReason.IllegalTransition && d.Intents == 1, "Release in Ready is illegal, no intent");
            });

            Case("5. Cancel cleans up safely", () =>
            {
                var d = new Driver(); d.Grab(); d.Move(0.12, Up1);
                Check(d.Gate.State == GateState.Armed, "armed before cancel");
                d.Time += 0.01;
                var c = d.Cancel();
                Check(c.State == GateState.Ready && c.Reason == GateReason.Cancelled && c.Evidence.SampleCount == 0 && d.Intents == 0, "Cancel in Armed -> Ready, evidence cleared, no intent");
                d.Time += 0.01;
                var rel = d.Release();
                Check(!rel.EventAccepted && rel.Reason == GateReason.IllegalTransition, "Release after Cancel is illegal (no stale Armed)");
                int seq = d.Gate.GrabSequence;
                d.Time += 0.01; d.Grab();
                Check(d.Gate.GrabSequence == seq + 1 && d.Gate.State == GateState.Grabbed, "re-grab opens a new grab sequence");
                var r = d.Release();
                Check(r.Reason == GateReason.FingerPlayRelease && d.Intents == 0, "immediate release after re-grab: finger play");
            });

            Case("6. Invalid samples never create a false toss", () =>
            {
                var lost = new Driver(); lost.Grab(); lost.Move(0.12, Up1);
                lost.Ownership = false;
                var r = lost.Move(1.0 / 72, Up1);
                Check(!r.EventAccepted && r.Reason == GateReason.OwnershipInvalid && r.State == GateState.Grabbed, "tracking lost while Armed -> disarmed");
                lost.Ownership = true;
                var rel = lost.Release();
                Check(rel.Reason == GateReason.FingerPlayRelease && lost.Intents == 0, "release right after lost tracking: finger play, not toss");

                var nan = new Driver(); nan.Grab(); nan.Move(0.04, Up1);
                nan.Time += 1.0 / 72;
                var bad = nan.Push(GateEvent.Sample(nan.Time, nan.Position, new Vector3(0f, float.NaN, 0f), nan.Frame));
                Check(!bad.EventAccepted && bad.Reason == GateReason.InvalidSample && bad.Evidence.SampleCount == 0, "NaN velocity rejected and run cleared");
                var after = nan.Move(1.0 / 72, Up1);
                Check(after.State == GateState.Grabbed && after.Evidence.SampleCount == 1, "evidence restarts from one sample");

                var glitch = new Driver(); glitch.Grab();
                var g = glitch.Move(0.12, new Vector3(0f, 50f, 0f));
                Check(!glitch.EverArmed && g.Reason == GateReason.InvalidSample, "50 m/s is a tracking glitch, not a toss");

                var frame = new Driver(); frame.Grab(); frame.Frame = new MotionFrame(1, Up1);
                var f = frame.Move(0.3, Up1);
                Check(!frame.EverArmed && f.Reason == GateReason.FrameMismatch, "samples in another frame are not consumed");
                frame.Frame = new MotionFrame(0, new Vector3(0f, 2f, 0f));
                Check(frame.Move(0.1, Up1).Reason == GateReason.FrameMismatch, "non-unit Up rejected");

                var fake = new Driver(); fake.Grab();
                var fk = fake.Move(0.3, Up1, movePosition: false);
                Check(!fake.EverArmed && fk.Evidence.UpwardDisplacement == 0 && fk.Evidence.MeanUpwardSpeed > 0.9,
                    "reported velocity without position change: displacement 0, never Armed");
            });

            Case("7. Lateral and downward motion are not tosses; the upward cone is explicit", () =>
            {
                var lateral = new Driver(); lateral.Grab(); lateral.Move(0.3, new Vector3(1.5f, 0.3f, 0f));
                Check(!lateral.EverArmed && lateral.Release().Reason == GateReason.FingerPlayRelease, "lateral 1.5 m/s with 0.3 up: ratio 0.2 < 0.5, never Armed");
                var down = new Driver(); down.Grab(); down.Move(0.3, new Vector3(0f, -1.2f, 0f));
                Check(!down.EverArmed && down.Release().Reason == GateReason.FingerPlayRelease, "downward 1.2 m/s: never Armed");
                var diag = new Driver(); diag.Grab(); diag.Move(0.12, new Vector3(0.9f, 0.9f, 0f));
                Check(diag.Gate.State == GateState.Armed, "45 degree lift (ratio 0.707) arms: cone edge is a parameter, not a product rule");
                var wide = new Driver(); wide.Grab(); wide.Move(0.2, new Vector3(1.0f, 0.5f, 0f));
                Check(!wide.EverArmed, "63 degrees from up (ratio 0.447) does not arm");
            });

            Case("8. Illegal transitions are rejected without side effects", () =>
            {
                var d = new Driver();
                Check(!d.Move(0.05, Up1).EventAccepted && d.Gate.State == GateState.Ready, "samples in Ready rejected");
                Check(d.Release().Reason == GateReason.IllegalTransition && d.Cancel().Reason == GateReason.IllegalTransition, "Release/Cancel in Ready illegal");
                d.Time += 0.01; d.Grab(); int seq = d.Gate.GrabSequence;
                d.Time += 0.01;
                var dupGrab = d.Grab();
                Check(!dupGrab.EventAccepted && dupGrab.Reason == GateReason.IllegalTransition && d.Gate.GrabSequence == seq, "GrabBegin while Grabbed rejected, sequence unchanged");
                d.Move(0.12, Up1); d.Release();
                Check(d.Gate.State == GateState.Flight, "in Flight");
                d.Time += 0.01;
                Check(!d.Grab().EventAccepted && !d.Move(0.05, Up1).EventAccepted && !d.Cancel().EventAccepted && d.Gate.State == GateState.Flight,
                    "Flight accepts nothing but Reset");
                Check(d.Reset().State == GateState.Ready, "Reset leaves Flight");
            });

            Case("9. Re-grab starts from clean history", () =>
            {
                // Two half-lifts that would arm if concatenated.
                var d = new Driver(); d.Grab(); d.Move(0.04, Up1);
                Check(!d.EverArmed, "0.04 s lift alone is not enough");
                d.Release(); d.Time += 0.01; d.Grab();
                Check(d.Log[d.Log.Count - 1].Evidence.SampleCount == 0, "evidence empty at re-grab");
                d.Move(0.04, Up1);
                Check(!d.EverArmed, "second 0.04 s lift after re-grab does not inherit the first");
                Check(d.Release().Reason == GateReason.FingerPlayRelease && d.Intents == 0, "no intent");

                var armed = new Driver(); armed.Grab(); armed.Move(0.12, Up1); armed.Time += 0.01; armed.Cancel();
                armed.Time += 0.01; armed.Grab(); armed.Still(0.05);
                var r = armed.Release();
                Check(r.Reason == GateReason.FingerPlayRelease && armed.Intents == 0, "Armed -> Cancel -> re-grab -> still release: no intent");
            });

            Case("10. Decision is stable across sampling rates", () =>
            {
                var armedTimes = new List<double>();
                foreach (double rate in new[] { 36.0, 60.0, 72.0, 90.0, 120.0 })
                {
                    var d = new Driver(); d.Grab(); d.Move(0.15, Up1, rate);
                    var r = d.Release();
                    Check(r.TossIntentAccepted && d.Intents == 1, rate + " Hz: one intent");
                    armedTimes.Add(d.ArmedAt);
                    Check(d.ArmedSampleCount >= 3, rate + " Hz: armed on >= 3 samples");
                }
                double spread = 0;
                foreach (var t in armedTimes) spread = Math.Max(spread, Math.Abs(t - armedTimes[2]));
                Check(spread <= 1.0 / 36 + 1e-9, "armed time spread across rates <= one slowest-rate period (" + spread.ToString("F4") + " s)");

                var edge = new Driver(); edge.Grab(); edge.Move(0.2, Up1, 20.0);
                Check(edge.Gate.State == GateState.Armed, "20 Hz (dt = MaxSampleGap) still continuous");
                var tooSlow = new Driver(); tooSlow.Grab(); var ts = tooSlow.Move(0.3, Up1, 15.0);
                Check(!tooSlow.EverArmed && ts.Reason == GateReason.ContinuityBroken, "15 Hz breaks continuity every sample: never Armed (rate floor is a parameter)");
            });

            Case("11. Timestamp anomalies fail safe", () =>
            {
                var d = new Driver(); d.Grab(); d.Move(0.12, Up1);
                Check(d.Gate.State == GateState.Armed, "armed");
                var dup = d.Push(GateEvent.Sample(d.Time, d.Position, Up1, d.Frame));
                Check(!dup.EventAccepted && dup.Reason == GateReason.InvalidTimestamp && dup.State == GateState.Grabbed, "duplicate timestamp: rejected, disarmed");
                var back = d.Push(GateEvent.Sample(d.Time - 0.5, d.Position, Up1, d.Frame));
                Check(!back.EventAccepted && back.Reason == GateReason.InvalidTimestamp, "backward timestamp rejected");
                var nanT = d.Push(GateEvent.Sample(double.NaN, d.Position, Up1, d.Frame));
                Check(!nanT.EventAccepted && nanT.Reason == GateReason.InvalidTimestamp, "NaN timestamp rejected");
                Check(d.Move(0.12, Up1).State == GateState.Armed, "clock recovers, evidence rebuilt from scratch");
                var relBack = d.Push(GateEvent.Release(d.Time - 1.0));
                Check(relBack.EventAccepted && relBack.State == GateState.Ready && !relBack.TossIntentAccepted && relBack.Reason == GateReason.InvalidTimestamp,
                    "Release with a bad clock still releases the coin but never counts as a toss");
                var g = new Driver();
                Check(!g.Push(GateEvent.GrabBegin(double.PositiveInfinity)).EventAccepted && g.Gate.State == GateState.Ready, "GrabBegin with Inf time rejected");
            });

            Case("12. Thresholds, release timing and the two Armed-hold candidates", () =>
            {
                var at = new Driver(); at.Grab(); at.Move(0.15, new Vector3(0f, 0.6f, 0f));
                Check(at.Gate.State == GateState.Armed, "mean exactly at arm speed (0.60) arms");
                var below = new Driver(); below.Grab(); below.Move(0.15, new Vector3(0f, 0.59f, 0f));
                Check(!below.EverArmed, "0.59 m/s does not arm");
                var dur = new Driver(); dur.Grab(); dur.Move(0.15, Up1);
                var armedResult = dur.Log.Find(r => r.Reason == GateReason.Armed);
                Check(armedResult.Evidence.SustainedSeconds >= 0.06 && armedResult.Evidence.SustainedSeconds < 0.06 + 1.0 / 72 + 1e-9,
                    "arms on the first sample whose span reaches MinEvidenceSeconds (" + armedResult.Evidence.SustainedSeconds.ToString("F4") + ")");

                // Release timing, RevokeOnEvidenceLoss (default).
                var grace = new Driver(); grace.Grab(); grace.Move(0.12, Up1); grace.Time += 0.09;
                Check(grace.Release().TossIntentAccepted, "release 0.09 s after last qualifying sample (within 0.10 grace) accepted");
                var late = new Driver(); late.Grab(); late.Move(0.12, Up1); late.Time += 0.11;
                var lr = late.Release();
                Check(!lr.TossIntentAccepted && lr.Reason == GateReason.ReleaseTooLate && lr.State == GateState.Ready, "release 0.11 s later: ReleaseTooLate, no intent");
                var stopped = new Driver(); stopped.Grab(); stopped.Move(0.12, Up1);
                var s1 = stopped.Still(1.0 / 72);
                Check(s1.Reason == GateReason.Disarmed && s1.State == GateState.Grabbed, "Revoke: one stopped frame disarms");
                Check(stopped.Release().Reason == GateReason.FingerPlayRelease && stopped.Intents == 0, "Revoke: lift-stop-release is finger play");

                // HoldForWindow candidate.
                var holdP = TossIntentParameters.Experimental(ArmedHoldPolicy.HoldForWindow, 0.15);
                var holdShort = new Driver(holdP); holdShort.Grab(); holdShort.Move(0.12, Up1);
                var h1 = holdShort.Still(0.10);
                Check(h1.Reason == GateReason.ArmedHeld && h1.State == GateState.Armed, "Hold: 0.10 s stopped still Armed (held)");
                Check(holdShort.Release().TossIntentAccepted, "Hold: release after 0.10 s stop accepted");
                var holdLong = new Driver(holdP); holdLong.Grab(); holdLong.Move(0.12, Up1);
                var h2 = holdLong.Still(0.30);
                Check(h2.State == GateState.Grabbed && holdLong.Log.Exists(r => r.Reason == GateReason.Disarmed), "Hold: 0.30 s stopped exceeds hold window, disarmed");
                Check(holdLong.Release().Reason == GateReason.FingerPlayRelease && holdLong.Intents == 0, "Hold: late release is finger play");
                var holdLost = new Driver(holdP); holdLost.Grab(); holdLost.Move(0.12, Up1); holdLost.Ownership = false;
                Check(holdLost.Move(1.0 / 72, Up1).State == GateState.Grabbed, "Hold: lost tracking still disarms immediately (hold never overrides safety)");
            });

            Case("13. Hold releases the original complete snapshot, never an empty run", () =>
            {
                var d = HeldDriver();
                var original = d.Log[d.Log.Count - 1].Evidence;
                d.Still(0.04);
                var r = d.Release();
                Check(r.TossIntentAccepted && CompleteEvidence(r.Evidence, d.Gate.Parameters), "held release retains full proof");
                Check(r.Evidence.Equals(original) && r.Intent.Value.Evidence.Equals(original), "snapshot unchanged by stationary samples");
            });

            Case("14. Hold cannot bridge gaps or refresh old qualification with one sample", () =>
            {
                var gap = HeldDriver(); gap.Still(1.0 / 72);
                gap.Time += 0.875;
                var afterGap = gap.Push(GateEvent.Sample(gap.Time, gap.Position, Up1, gap.Frame));
                Check(afterGap.Reason == GateReason.ContinuityBroken && !afterGap.IsArmed, "0.875 s gap revokes Held Armed independently of run count");
                Check(!gap.Release().TossIntentAccepted, "single post-gap sample cannot authorize Release");

                var stale = HeldDriver(); double snapshotTime = stale.Time;
                stale.Still(0.08); stale.Move(1.0 / 72, Up1);
                stale.Time = snapshotTime + 0.11;
                Check(!stale.Release().TossIntentAccepted, "one new upward sample cannot refresh the old release deadline");

                var repeat = HeldDriver(); repeat.Still(0.30);
                Check(!repeat.Log[repeat.Log.Count - 1].IsArmed && !repeat.Release().TossIntentAccepted,
                    "continuous nonqualifying samples cannot renew Hold");

                var rebuild = HeldDriver(); rebuild.Still(0.30); rebuild.Move(1.0 / 72, Up1);
                Check(rebuild.Gate.State == GateState.Grabbed, "expired snapshot cannot be revived by one sample");
                rebuild.Move(0.12, Up1);
                Check(rebuild.Release().TossIntentAccepted, "a complete new continuous segment may qualify independently");
            });

            Case("15. Hold and ReleaseGrace are both required, including sample-free release", () =>
            {
                var grace = HeldDriver(); grace.Time += 0.11;
                Check(!grace.Release().TossIntentAccepted, "inside 0.15 Hold but outside 0.10 Grace must reject");
                var hold = HeldDriver(0.04); hold.Time += 0.05;
                Check(!hold.Release().TossIntentAccepted, "inside Grace but outside short Hold must reject");
                var expired = HeldDriver(0.04); expired.Still(0.06);
                Check(!expired.Log[expired.Log.Count - 1].IsArmed, "Hold expiry revokes even before Grace expiry");
                var boundary = HeldDriver(0.125);
                boundary.Time += 0.10;
                Check(boundary.Release().TossIntentAccepted, "Grace equality accepts without a new sample");
                var exactHold = HeldDriver(0.03125); exactHold.Time += 0.03125;
                Check(exactHold.Release().TossIntentAccepted, "Hold equality accepts");
                var sameFrame = HeldDriver();
                Check(sameFrame.Release().TossIntentAccepted, "same-frame Release remains legal");
            });

            Case("16. Safety invalidation clears snapshots and reset does not recycle grab IDs", () =>
            {
                foreach (string kind in new[] { "ownership", "frame", "up-change", "timestamp", "position", "velocity" })
                {
                    var d = HeldDriver(); d.Still(1.0 / 72);
                    double t = kind == "timestamp" ? d.Time : d.Time + 1.0 / 72;
                    var frame = kind == "frame" ? new MotionFrame(99, Up1)
                        : kind == "up-change" ? new MotionFrame(0, -Up1) : d.Frame;
                    var pos = kind == "position" ? new Vector3(float.PositiveInfinity, 0, 0) : d.Position;
                    var vel = kind == "velocity" ? new Vector3(0, float.NaN, 0) : Up1;
                    var r = d.Push(GateEvent.Sample(t, pos, vel, frame, kind != "ownership"));
                    Check(!r.IsArmed && !r.EventAccepted, kind + ": invalidates held evidence");
                    d.Time = t;
                    Check(!d.Release().TossIntentAccepted, kind + ": no accepted intent after invalidation");
                }
                var reset = HeldDriver(); var first = reset.Release();
                reset.Reset(); reset.Grab(); reset.Move(0.12, Up1); var second = reset.Release();
                Check(second.Intent.Value.IntentId > first.Intent.Value.IntentId, "same-instance Reset preserves monotonic intent identity");
            });

            Console.WriteLine("OFFLINE RESULT: " + _passed + "/" + _cases + " cases passed, assertions=" + _assertions
                + ", failed assertions=" + Failures.Count);
            Console.WriteLine("RULES ON SYNTHETIC INPUT ONLY | P2 TOSS INTENT ISOLATED PROTOTYPE | NOT INTEGRATED | NOT RUNTIME VALIDATED | NO RNG / NO PHYSICS FLIGHT");
            return Failures.Count == 0 ? 0 : 1;
        }
    }
}
