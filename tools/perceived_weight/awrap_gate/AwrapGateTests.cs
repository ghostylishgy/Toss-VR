// Cases A-I for the P1.3 A-wrap gate. Deterministic, no Unity, no SDK, no XR input.
using System;
using System.Collections.Generic;
using System.Numerics;
using Toss.Experiments.PerceivedWeight;

namespace Toss.Experiments.AwrapGate
{
    public static class AwrapGateTests
    {
        private const double FrameDt = 1.0 / 72.0;
        private const double MaxLag = 0.004;
        private static readonly List<string> Failures = new List<string>();
        private static int _assertions, _cases, _passedCases;

        private sealed class Rig
        {
            public readonly GrabbableDouble Grabbable = new GrabbableDouble();
            public readonly GrabFreeTransformerDouble Inner = new GrabFreeTransformerDouble();
            public readonly CoinFollowCoreProto Wrapper;
            public double Now;
            public readonly List<PointerEventType> Raised = new List<PointerEventType>();

            public Rig(FollowMode? mode, bool naive = false)
            {
                Grabbable.Transform.Write(new FollowPose(new Vector3(0f, 1f, 0.3f), Quaternion.Identity));
                Grabbable.Transform.Writes.Clear();
                if (mode == null)
                {
                    Grabbable.InjectOptionalOneGrabTransformer(Inner);
                }
                else
                {
                    Wrapper = new CoinFollowCoreProto(Inner, Grabbable.Transform, () => Now, mode.Value, naive);
                    Grabbable.InjectOptionalOneGrabTransformer(Wrapper);
                }
                Grabbable.WhenPointerEventRaised += e => Raised.Add(e.Type);
                Grabbable.Start();
            }

            public void Hover(FollowPose p) { Grabbable.ProcessPointerEvent(new PointerEvent(1, PointerEventType.Hover, p)); }
            public void Select(FollowPose p) { Grabbable.ProcessPointerEvent(new PointerEvent(1, PointerEventType.Select, p)); }
            public void Move(FollowPose p) { Grabbable.ProcessPointerEvent(new PointerEvent(1, PointerEventType.Move, p)); }
            public void Unselect(FollowPose p) { Grabbable.ProcessPointerEvent(new PointerEvent(1, PointerEventType.Unselect, p)); }
            public void Cancel(FollowPose p) { Grabbable.ProcessPointerEvent(new PointerEvent(1, PointerEventType.Cancel, p)); }
            public int Writes => Grabbable.Transform.Writes.Count;
            public FollowPose Pose => Grabbable.Transform.Pose;
        }

        private static FollowPose Hand(double t)
        {
            // 0.5 m/s along +x with a slow yaw; grab point starts 2 cm from the coin centre.
            var pos = new Vector3(0.02f + (float)(0.5 * t), 1f, 0.3f);
            var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)(0.8 * t));
            return new FollowPose(pos, rot);
        }

        private static FollowPose Offset(FollowPose p, Vector3 d) { return new FollowPose(p.Position + d, p.Rotation); }

        private static double Dist(FollowPose a, FollowPose b) { return (a.Position - b.Position).Length(); }

        private static double AngleDeg(Quaternion a, Quaternion b)
        {
            float dot = Math.Abs(Quaternion.Dot(Quaternion.Normalize(a), Quaternion.Normalize(b)));
            return 2 * Math.Acos(Math.Min(1f, dot)) * 180 / Math.PI;
        }

        private static bool SamePose(FollowPose a, FollowPose b)
        {
            return a.Position == b.Position && a.Rotation == b.Rotation;
        }

        private static void Check(bool ok, string what)
        {
            _assertions++;
            if (!ok) Failures.Add(what);
        }

        private static void Case(string name, Action body)
        {
            _cases++;
            int before = Failures.Count;
            try { body(); }
            catch (Exception e) { Failures.Add(name + ": unexpected exception " + e.GetType().Name + ": " + e.Message); }
            if (Failures.Count == before) { _passedCases++; Console.WriteLine("PASS " + name); }
            else
            {
                Console.WriteLine("FAIL " + name);
                for (int i = before; i < Failures.Count; i++) Console.WriteLine("    " + Failures[i]);
            }
        }

        // Drive a grab in both rigs for n frames; returns the last hand pose.
        private static FollowPose Drive(Rig rig, int frames)
        {
            rig.Now = 0;
            rig.Hover(Hand(0));
            rig.Select(Hand(0));
            FollowPose hand = Hand(0);
            for (int i = 1; i <= frames; i++)
            {
                rig.Now = i * FrameDt;
                hand = Hand(rig.Now);
                rig.Move(hand);
            }
            return hand;
        }

        public static int Main(string[] args)
        {
            Console.WriteLine("P1.3 A-WRAP PRE-INTEGRATION GATE | OFFLINE DOUBLES | NOT UNITY | NOT SDK | NOT XR");

            Case("A first grab initialises from current pose (Rigid and Spring)", () =>
            {
                foreach (var mode in new[] { FollowMode.Rigid, FollowMode.SpringDamper })
                {
                    var rig = new Rig(mode);
                    var start = rig.Pose;
                    rig.Hover(Hand(0));
                    rig.Select(Hand(0));
                    Check(rig.Inner.InitializeCount == 1, mode + ": inner Initialize exactly once");
                    Check(rig.Inner.BeginCount == 1 && rig.Inner.EndCount == 0, mode + ": Select -> one inner Begin, no End");
                    Check(rig.Writes == 0, mode + ": Select writes nothing (" + rig.Writes + ")");
                    Check(SamePose(rig.Wrapper.FollowerOutput, start), mode + ": follower reset to current pose");
                    Check(rig.Wrapper.LinearVelocity == Vector3.Zero && rig.Wrapper.AngularVelocity == Vector3.Zero,
                        mode + ": zero velocity at grab start");
                    Check(rig.Raised.Count == 2 && rig.Raised[1] == PointerEventType.Select, mode + ": harness sees Select before Begin");
                    rig.Now = FrameDt;
                    rig.Move(Hand(rig.Now));
                    var target = Hand(rig.Now);
                    var inner = rig.Grabbable.Transform.Writes[0];
                    Check(Dist(inner, target) > 0.019 && Dist(inner, target) < 0.021, mode + ": inner keeps 2 cm grab offset");
                    if (mode == FollowMode.Rigid)
                        Check(rig.Writes == 1, "Rigid: first Move writes once (" + rig.Writes + ")");
                    else
                    {
                        Check(rig.Writes == 2, "Spring: first Move writes inner + follower (" + rig.Writes + ")");
                        Check(Dist(rig.Pose, inner) > 0 && Dist(rig.Pose, inner) <= MaxLag + 1e-6, "Spring: lags within 4 mm");
                    }
                }
            });

            Case("B normal per-frame update: bounded lag, 1 (Rigid) / 2 (Spring) writes per Move", () =>
            {
                var rigid = new Rig(FollowMode.Rigid);
                var spring = new Rig(FollowMode.SpringDamper);
                Drive(rigid, 36);
                Drive(spring, 36);
                Check(rigid.Writes == 36, "Rigid writes == 36 (" + rigid.Writes + ")");
                Check(spring.Writes == 72, "Spring writes == 72 (" + spring.Writes + ")");
                double maxLag = 0, maxAng = 0;
                var w = spring.Grabbable.Transform.Writes;
                for (int i = 0; i < w.Count; i += 2)
                {
                    maxLag = Math.Max(maxLag, Dist(w[i], w[i + 1]));
                    maxAng = Math.Max(maxAng, AngleDeg(w[i].Rotation, w[i + 1].Rotation));
                }
                Check(maxLag > 0 && maxLag <= MaxLag + 1e-6, "Spring max lag " + maxLag + " <= 4 mm");
                Check(maxAng <= 5 + 1e-3, "Spring max angular lag " + maxAng + " deg <= 5");
                Check(spring.Wrapper.LinearVelocity.Length() > 0.01f, "Spring has built inertia (" + spring.Wrapper.LinearVelocity.Length() + ")");
                Check(spring.Wrapper.SameFrameCarries == 0 && spring.Wrapper.TimeGapResets == 0, "no same-frame or gap paths at 72 Hz");
            });

            Case("C two Moves in one frame: second target honoured, Step not double-counted", () =>
            {
                var contract = new Rig(FollowMode.SpringDamper);
                var naive = new Rig(FollowMode.SpringDamper, naive: true);
                var last = Drive(contract, 36);
                Drive(naive, 36);
                var velocityBefore = contract.Wrapper.LinearVelocity;
                var outputBefore = contract.Pose;
                var innerBefore = contract.Grabbable.Transform.Writes[contract.Writes - 2];
                var second = Offset(last, new Vector3(0f, 0.03f, 0f));
                contract.Move(second);
                naive.Move(second);
                var innerAfter = contract.Grabbable.Transform.Writes[contract.Writes - 2];
                var carried = outputBefore.Position - innerBefore.Position;
                Check(contract.Wrapper.SameFrameCarries == 1, "contract took carry path once");
                Check((contract.Pose.Position - (innerAfter.Position + carried)).Length() < 1e-6f,
                    "contract output = new target + carried lag error");
                Check(Dist(contract.Pose, innerAfter) <= MaxLag + 1e-6, "contract stays within 4 mm of new target");
                Check(contract.Wrapper.LinearVelocity == velocityBefore, "contract velocity untouched by same-frame repeat");
                Check(Dist(naive.Pose, innerAfter) > 0.029, "RISK: naive Step(dt=0) ignores the second target (" + Dist(naive.Pose, innerAfter) + " m away)");
            });

            Case("D Move then Unselect same frame: ForceMove target carried, Begin/End paired, authority released", () =>
            {
                foreach (var mode in new[] { FollowMode.Rigid, FollowMode.SpringDamper })
                {
                    var rig = new Rig(mode);
                    var last = Drive(rig, 36);
                    int writesBefore = rig.Writes;
                    var release = Offset(last, new Vector3(0.004f, 0.002f, 0f));
                    rig.Unselect(release);
                    int writesInRelease = rig.Writes - writesBefore;
                    var innerFinal = rig.Grabbable.Transform.Writes[mode == FollowMode.Rigid ? rig.Writes - 1 : rig.Writes - 2];
                    Check(rig.Inner.BeginCount == 1 && rig.Inner.EndCount == 1, mode + ": inner Begin==End==1 after Unselect");
                    Check(!rig.Grabbable.HasActiveTransformer, mode + ": Grabbable active transformer cleared");
                    Check(!rig.Wrapper.Began, mode + ": wrapper not begun");
                    Check(rig.Raised[rig.Raised.Count - 1] == PointerEventType.Unselect
                        && rig.Raised[rig.Raised.Count - 2] == PointerEventType.Move, mode + ": harness sees ForceMove Move then Unselect");
                    Check(Dist(innerFinal, release) > 0.019 && Dist(innerFinal, release) < 0.021, mode + ": SDK final target uses release pose");
                    if (mode == FollowMode.Rigid)
                    {
                        Check(writesInRelease == 1, "Rigid: ForceMove produces exactly 1 write (" + writesInRelease + ")");
                        Check(SamePose(rig.Pose, innerFinal), "Rigid: final pose == SDK final target");
                    }
                    else
                    {
                        Check(writesInRelease == 2, "Spring: ForceMove produces 2 writes (" + writesInRelease + ")");
                        Check(rig.Wrapper.SameFrameCarries == 1, "Spring: dt=0 carry path taken once");
                        Check(Dist(rig.Pose, innerFinal) <= MaxLag + 1e-6, "Spring: final pose within 4 mm of final target (" + Dist(rig.Pose, innerFinal) + ")");
                        Check(SamePose(rig.Wrapper.FollowerOutput, rig.Pose), "Spring: follower reset to final written pose");
                        Check(rig.Wrapper.LinearVelocity == Vector3.Zero, "Spring: velocity cleared on End");
                    }
                }
            });

            Case("E ForceMove target far from last output: contract follows, naive drops it", () =>
            {
                var contract = new Rig(FollowMode.SpringDamper);
                var naive = new Rig(FollowMode.SpringDamper, naive: true);
                var last = Drive(contract, 36);
                Drive(naive, 36);
                var release = Offset(last, new Vector3(0f, 0f, 0.05f));
                contract.Unselect(release);
                naive.Unselect(release);
                var target = contract.Grabbable.Transform.Writes[contract.Writes - 2];
                Check(Dist(contract.Pose, target) <= MaxLag + 1e-6, "contract final within 4 mm of 5 cm-displaced target");
                Check(Dist(naive.Pose, target) > 0.049, "RISK: naive final is " + Dist(naive.Pose, target) + " m from SDK final target");
                Check(naive.Inner.BeginCount == 1 && naive.Inner.EndCount == 1, "naive still pairs Begin/End (risk is pose, not lifecycle)");
            });

            Case("F Cancel then immediate re-grab: no inertia carried", () =>
            {
                var rig = new Rig(FollowMode.SpringDamper);
                var last = Drive(rig, 36);
                Check(rig.Wrapper.LinearVelocity.Length() > 0.01f, "inertia present before Cancel");
                int writesBefore = rig.Writes;
                rig.Cancel(last);
                Check(rig.Writes == writesBefore, "Cancel writes nothing");
                Check(rig.Inner.BeginCount == 1 && rig.Inner.EndCount == 1, "Cancel pairs inner End");
                Check(rig.Wrapper.LinearVelocity == Vector3.Zero && rig.Wrapper.AngularVelocity == Vector3.Zero, "velocity zero after Cancel");
                Check(rig.Raised[rig.Raised.Count - 1] == PointerEventType.Cancel, "harness sees Cancel after End");
                var regrab = Offset(last, new Vector3(0f, 0.01f, 0f));
                rig.Hover(regrab);
                rig.Select(regrab);
                Check(rig.Inner.BeginCount == 2 && rig.Inner.EndCount == 1, "re-grab Begin counted, still paired");
                rig.Now += FrameDt;
                var next = Offset(regrab, new Vector3(0.007f, 0f, 0f));
                rig.Move(next);

                var fresh = new Rig(FollowMode.SpringDamper);
                fresh.Grabbable.Transform.Write(rig.Grabbable.Transform.Writes[writesBefore - 1]);
                fresh.Grabbable.Transform.Writes.Clear();
                fresh.Now = rig.Now - FrameDt;
                fresh.Hover(regrab);
                fresh.Select(regrab);
                fresh.Now = rig.Now;
                fresh.Move(next);
                Check(SamePose(fresh.Pose, rig.Pose), "post-cancel re-grab output identical to a fresh zero-velocity follower");
            });

            Case("G Rigid wrapper output bit-identical to SDK-native transformer", () =>
            {
                var native = new Rig(null);
                var rigid = new Rig(FollowMode.Rigid);
                var last = Drive(native, 36);
                Drive(rigid, 36);
                native.Move(Offset(last, new Vector3(0f, 0.03f, 0f)));
                rigid.Move(Offset(last, new Vector3(0f, 0.03f, 0f)));
                native.Unselect(Offset(last, new Vector3(0.004f, 0f, 0f)));
                rigid.Unselect(Offset(last, new Vector3(0.004f, 0f, 0f)));
                var a = native.Grabbable.Transform.Writes;
                var b = rigid.Grabbable.Transform.Writes;
                Check(a.Count == b.Count, "write counts equal (" + a.Count + " vs " + b.Count + ")");
                bool identical = a.Count == b.Count;
                for (int i = 0; identical && i < a.Count; i++) identical = SamePose(a[i], b[i]);
                Check(identical, "every write bit-identical");
                Check(native.Inner.BeginCount == rigid.Inner.BeginCount && native.Inner.EndCount == rigid.Inner.EndCount
                    && native.Inner.UpdateCount == rigid.Inner.UpdateCount, "inner call counts equal");
            });

            Case("H time gap > 0.1 s resets to target without inertia", () =>
            {
                var rig = new Rig(FollowMode.SpringDamper);
                var last = Drive(rig, 36);
                rig.Now += 0.2;
                var far = Offset(last, new Vector3(0.1f, 0f, 0f));
                rig.Move(far);
                var target = rig.Grabbable.Transform.Writes[rig.Writes - 2];
                Check(rig.Wrapper.TimeGapResets == 1 && (rig.Wrapper.LastFlags & FollowFlags.TimeGapReset) != 0, "TimeGapReset flagged");
                Check(SamePose(rig.Pose, target), "output snapped to target");
                Check(rig.Wrapper.LinearVelocity == Vector3.Zero, "velocity cleared by gap reset");
                rig.Now += FrameDt;
                rig.Move(Offset(far, new Vector3(0.007f, 0f, 0f)));
                Check((rig.Wrapper.LastFlags & FollowFlags.TimeGapReset) == 0, "normal step resumes after gap");
                Check(rig.Writes == 76, "two more Moves -> 4 more writes (" + rig.Writes + ")");
            });

            Case("I Begin/End pairing anomalies: wrapper guards, inner contract mirrors SDK", () =>
            {
                var bare = new GrabFreeTransformerDouble();
                bool threw = false;
                try { bare.EndTransform(); } catch (ArgumentNullException) { threw = true; }
                Check(threw, "bare GrabFreeTransformer double: End without Begin throws ArgumentNullException (ArrayPool.Return(null))");

                var rig = new Rig(FollowMode.SpringDamper);
                rig.Wrapper.EndTransform();
                Check(rig.Wrapper.EndWithoutBegin == 1 && rig.Inner.EndCount == 0, "wrapper End without Begin: ignored, not forwarded");
                rig.Wrapper.UpdateTransform();
                Check(rig.Wrapper.UpdateWithoutBegin == 1 && rig.Inner.UpdateCount == 0 && rig.Writes == 0, "wrapper Update without Begin: ignored, no write");

                rig.Hover(Hand(0));
                rig.Select(Hand(0));
                rig.Wrapper.BeginTransform();
                Check(rig.Wrapper.BeginWhileBegun == 1 && rig.Inner.BeginCount == 2 && rig.Inner.EndCount == 1 && rig.Inner.LeakedRents == 0,
                    "wrapper Begin while begun: inner End forwarded first, no pool leak");
                rig.Now = FrameDt;
                rig.Move(Hand(rig.Now));
                rig.Grabbable.OnDisable();
                Check(rig.Inner.BeginCount == 2 && rig.Inner.EndCount == 2 && !rig.Wrapper.Began, "OnDisable: Grabbable ends once, Cancel does not end again");
                Check(rig.Raised[rig.Raised.Count - 1] == PointerEventType.Cancel && rig.Grabbable.GrabPoints.Count == 0, "OnDisable cancels selecting point after End");
                Check(rig.Wrapper.LinearVelocity == Vector3.Zero, "OnDisable leaves no inertia");

                // Exception isolation: a non-finite target must not throw out of UpdateTransform, otherwise
                // Grabbable.ProcessPointerEvent(Unselect) would abort before EndTransform/base.Unselect.
                var guarded = new Rig(FollowMode.SpringDamper);
                var naive = new Rig(FollowMode.SpringDamper, naive: true); // naive = no dt=0 carry and no finiteness guard
                var last = Drive(guarded, 10);
                Drive(naive, 10);
                var nan = new FollowPose(new Vector3(float.NaN, 1f, 0.3f), last.Rotation);
                guarded.Now += FrameDt;
                naive.Now += FrameDt;
                guarded.Unselect(nan);
                Check(guarded.Wrapper.PassThroughFallbacks == 1 && guarded.Inner.BeginCount == 1 && guarded.Inner.EndCount == 1
                    && guarded.Grabbable.GrabPoints.Count == 0, "guarded: NaN release falls back to pass-through, Unselect completes");
                bool naiveThrew = false;
                try { naive.Unselect(nan); } catch (ArgumentException) { naiveThrew = true; }
                Check(naiveThrew && naive.Inner.EndCount == 0 && naive.Grabbable.GrabPoints.Count == 1 && naive.Grabbable.HasActiveTransformer,
                    "RISK: unguarded Step throw during ForceMove aborts Unselect -> grab stuck, inner never Ended");
            });

            Console.WriteLine("OFFLINE RESULT: " + _passedCases + "/" + _cases + " cases passed, assertions=" + _assertions
                + ", failed assertions=" + Failures.Count);
            Console.WriteLine("OFFLINE DOUBLES ONLY | NOT UNITY | NOT META SDK | NOT XR | RUNTIME UNVERIFIED");
            return Failures.Count == 0 ? 0 : 1;
        }
    }
}
