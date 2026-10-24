// OFFLINE REGRESSION HARNESS ONLY: executes production methods extracted verbatim from
// Assets/Scripts/Coin/FingerPlayHarness.cs against deterministic doubles.
// This proves arithmetic and state-clearing behaviour off-engine. It is NOT Unity physics,
// NOT real SDK event ordering, NOT XR tracking and NOT subjective-feel validation.
using System;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;
using TossMotionAudit;

namespace Toss.Coin
{
    public partial class FingerPlayHarness
    {
        private const int Owner = 7;
        private static int cases, failedCases, assertions, failedAssertions, globalKinematicWrites;
        private static readonly List<string> failures = new List<string>();
        private static readonly List<string> notes = new List<string>();

        public static int Main()
        {
            Console.WriteLine("P1.2C MOTION SAMPLING OFFLINE REGRESSION | DOUBLES ONLY | NOT ENGINE/SDK/XR VALIDATION | IMPLEMENTATION CHECK, RUNTIME PENDING");
            Case("zero single sample", ZeroAndSingleSample);
            Case("stationary", Stationary);
            Case("constant velocity", ConstantVelocity);
            Case("non-uniform intervals", NonUniformIntervals);
            Case("tiny dt threshold", TinyDeltaTime);
            Case("large dt", LargeDeltaTime);
            Case("sudden reversal", SuddenReversal);
            Case("queue boundary", QueueBoundary);
            Case("quaternion wrap", QuaternionWrap);
            Case("release clamp", ReleaseClamp);
            Case("non-finite input", NonFiniteInput);
            Case("history contamination", HistoryContamination);
            Console.WriteLine("OFFLINE RESULT: {0}/{1} cases passed, assertions={2}, failed assertions={3}, kinematic velocity writes={4}",
                cases - failedCases, cases, assertions, failedAssertions, globalKinematicWrites);
            foreach (string note in notes) Console.WriteLine("NOTE " + note);
            foreach (string failure in failures) Console.WriteLine("  FAILURE " + failure);
            Console.WriteLine("ASSERTION: production file is read-only for these tests; see the runner manifest hashes");
            return failedCases == 0 ? 0 : 1;
        }

        // Case names are stable keys: the runner matches deliberate defect injections against them.
        private static void Case(string name, Action test)
        {
            cases++;
            Audit.Reset();
            failures.Clear();
            try { test(); }
            catch (Exception e) { failures.Add("EXCEPTION " + e); }
            // Production must never write velocity into a kinematic Rigidbody; the double counts it.
            Check(Audit.KinematicVelocityWrites == 0, "velocity written while kinematic: " + Audit.KinematicVelocityWrites);
            globalKinematicWrites += Audit.KinematicVelocityWrites;
            failedAssertions += failures.Count;
            Console.WriteLine((failures.Count == 0 ? "PASS " : "FAIL ") + name);
            foreach (string failure in failures) Console.WriteLine("  " + failure);
            if (failures.Count > 0) failedCases++;
        }

        private static void Check(bool ok, string message)
        {
            assertions++;
            if (!ok) failures.Add(message);
        }

        private static void Near(double actual, double expected, double tolerance, string message)
        {
            Check(Math.Abs(actual - expected) <= tolerance,
                message + ": actual=" + actual.ToString("R") + ", expected=" + expected.ToString("R") + ", tolerance=" + tolerance);
        }

        private static void Vec(Vector3 actual, double x, double y, double z, double tolerance, string message)
        {
            Near(actual.x, x, tolerance, message + " x");
            Near(actual.y, y, tolerance, message + " y");
            Near(actual.z, z, tolerance, message + " z");
        }

        private static bool Finite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        private static Vector3 Axis(float x, float y, float z) { return new Vector3(x, y, z).normalized; }

        private static FingerPlayHarness Held(ReleaseProfile profile = ReleaseProfile.ResidualSettle)
        {
            var h = new FingerPlayHarness();
            h._releaseProfile = profile;
            h._rigidbody = new Rigidbody();
            h._coinPresenter = new CoinPresenter();
            h._grabbable = new Grabbable();
            h._rigidbody.isKinematic = true;
            Audit.Events.Clear();
            h.OnGrabBegin(new PointerEvent(Owner, PointerEventType.Select, new Pose(Vector3.zero, Quaternion.identity)));
            Check(h._state == FingerPlayState.Grabbed, "fixture starts grabbed");
            Check(h._motionSamples.Count == 0 && !h._hasPrevSample, "grab clears sampling history");
            h.transform.position = Vector3.zero;
            h.transform.rotation = Quaternion.identity;
            return h;
        }

        private static void PoseFrame(FingerPlayHarness h, Vector3 position, Quaternion rotation, float dt)
        {
            Time.deltaTime = dt;
            h.transform.position = position;
            h.transform.rotation = rotation;
            h.UpdateMotionSampling();
        }

        private static MotionSample LastSample(FingerPlayHarness h)
        {
            var buffer = new MotionSample[h._motionSamples.Count];
            h._motionSamples.CopyTo(buffer, 0);
            return buffer[buffer.Length - 1];
        }

        private static void Estimate(FingerPlayHarness h, out Vector3 linear, out Vector3 angular)
        {
            h.GetEstimatedVelocities(out linear, out angular);
            Check(Finite(linear), "estimated linear velocity is finite: " + linear);
            Check(Finite(angular), "estimated angular velocity is finite: " + angular);
        }

        private static void ZeroAndSingleSample()
        {
            var idle = new FingerPlayHarness();
            Vector3 l, a;
            idle.GetEstimatedVelocities(out l, out a);
            Vec(l, 0, 0, 0, 0, "empty queue linear");
            Vec(a, 0, 0, 0, 0, "empty queue angular");
            Check(idle.GetSelectingPointsCount() == 0, "no grabbable reports zero selections");
            var empty = new FingerPlayHarness();
            empty._grabbable = new Grabbable();
            empty._grabbable.SelectingPoints = null;
            Check(empty.GetSelectingPointsCount() == 0, "null selection list reports zero");

            var h = Held();
            Check(h._currentOwnerId == Owner, "owner recorded");
            PoseFrame(h, new Vector3(1, 2, 3), Quaternion.AxisAngle(Vector3.up, 0.4f), 0.02f);
            Check(h._motionSamples.Count == 0, "first sampled frame only seeds the previous pose");
            Check(h._hasPrevSample, "first frame marks the previous pose as stored");
            Vec(h._prevSamplePosition, 1, 2, 3, 0, "seeded previous position");
            Near(h._prevSampleRotation.sqrMagnitude, 1, 1e-6, "seeded previous rotation is unit length");
            Estimate(h, out l, out a);
            Vec(l, 0, 0, 0, 0, "estimate before any sample is zero");

            PoseFrame(h, new Vector3(1.5f, 2, 3), Quaternion.identity, 0.02f);
            Check(h._motionSamples.Count == 1, "exactly one accepted sample");
            Estimate(h, out l, out a);
            Vec(l, 25, 0, 0, 1e-4, "single-sample estimate equals that sample (0.5m / 0.02s)");
            // The seed rotation was 0.4 rad about +y, so identity is a -0.4 rad change over one 0.02 s frame.
            Vec(a, 0, -20, 0, 1e-3, "single-sample angular estimate divides the 0.4 rad change by dt");
            Check(Math.Abs(a.y) > 19.9, "a rotation change is not silently dropped at the single-sample stage");

            h.OnGrabCancel(new PointerEvent(Owner, PointerEventType.Cancel, new Pose()));
            h.OnGrabBegin(new PointerEvent(Owner, PointerEventType.Select, new Pose()));
            Check(!h._hasPrevSample && h._motionSamples.Count == 0, "regrab reseeds sampling");
            PoseFrame(h, new Vector3(9, 9, 9), Quaternion.identity, 0.02f);
            Check(h._motionSamples.Count == 0, "first frame after regrab seeds only");
        }

        private static void Stationary()
        {
            foreach (ReleaseProfile profile in new[] { ReleaseProfile.Freeze, ReleaseProfile.ResidualSettle })
            {
                var h = Held(profile);
                Vector3 still = new Vector3(0.0123f, -0.0456f, 0.0789f);
                Quaternion rot = Quaternion.AxisAngle(Axis(1, 2, -2), 0.7f);
                PoseFrame(h, still, rot, 0.02f);
                for (int i = 0; i < 200; i++) PoseFrame(h, still, rot, i % 3 == 0 ? 0.004f : 0.033f);
                Check(h._motionSamples.Count == 6, "stationary sampling fills the queue with zero-velocity samples");
                foreach (MotionSample s in h._motionSamples)
                {
                    Vec(s.LinearVelocity, 0, 0, 0, 0, "stationary sample linear");
                    Vec(s.AngularVelocity, 0, 0, 0, 0, "stationary sample angular");
                    Check(!float.IsNaN(s.DeltaTime) && !float.IsInfinity(s.DeltaTime) && s.DeltaTime > 0,
                        "stationary sample dt is positive and finite");
                }
                Vector3 l, a;
                Estimate(h, out l, out a);
                Vec(l, 0, 0, 0, 0, "stationary estimate linear");
                Vec(a, 0, 0, 0, 0, "stationary estimate angular");
                h.OnGrabEnd(new PointerEvent(Owner, PointerEventType.Unselect, new Pose()));
                Vec(h._rigidbody.linearVelocity, 0, 0, 0, 0, "stationary release linear");
                Vec(h._rigidbody.angularVelocity, 0, 0, 0, 0, "stationary release angular");
                Check(profile == ReleaseProfile.Freeze || h._state == FingerPlayState.Settling,
                    "a stationary release still enters the settle path with zero velocity");
            }
            notes.Add("Exact zeros above depend on the doubles: q * Inverse(q) has an exactly zero vector part, so the "
                + "axis guard reports zero angular velocity. Native Unity quaternion math can leave a sub-micro-radian "
                + "angle whose divide-by-dt can reach ~1e-1 rad/s; that engine-side rounding is NOT reproduced here.");
        }

        private static void ConstantVelocity()
        {
            var velocity = new Vector3(0.25f, -0.1f, 0.05f);
            var axis = Axis(1, 2, -1);
            const double omega = 1.5;
            var h = Held();
            double t = 0;
            PoseFrame(h, Vector3.zero, Quaternion.AxisAngle(axis, 0), 0.02f);
            for (int i = 0; i < 40; i++)
            {
                t += 0.02;
                PoseFrame(h, velocity * (float)t, Quaternion.AxisAngle(axis, (float)(omega * t)), 0.02f);
                Vector3 l, a;
                Estimate(h, out l, out a);
                if (i >= 6)
                {
                    Vec(l, velocity.x, velocity.y, velocity.z, 2e-4, "steady linear estimate");
                    // acos near w=1 amplifies float rounding, so the angular tolerance is loose by design.
                    Vec(a, axis.x * omega, axis.y * omega, axis.z * omega, 2e-3, "steady angular estimate");
                }
                if (i == 6)
                {
                    foreach (MotionSample s in h._motionSamples)
                    {
                        Near(s.LinearVelocity.x, 0.25, 2e-3, "per-sample linear velocity matches the frame delta");
                        Check(s.LinearVelocity.x > 0, "forward motion keeps a forward sample");
                    }
                }
            }
            Check(h._motionSamples.Count == 6, "steady sampling stays capped at six");
            double total = 0;
            foreach (MotionSample s in h._motionSamples) total += s.LinearVelocity.magnitude;
            Check(total > 0, "samples carry the true speed");
        }

        private static void NonUniformIntervals()
        {
            double[] intervals = { 0.02, 0.005, 0.05, 0.01, 0.025, 0.04 };
            var direction = new Vector3(1, 0.5f, -0.25f).normalized;
            const double acceleration = 2.0;
            var h = Held();
            double t = 0;
            PoseFrame(h, Vector3.zero, Quaternion.identity, 0.02f);
            var averages = new List<double>();
            foreach (double dt in intervals)
            {
                double from = t, to = t + dt;
                t = to;
                PoseFrame(h, direction * (float)(0.5 * acceleration * to * to), Quaternion.identity, (float)dt);
                averages.Add((0.5 * acceleration * to * to - 0.5 * acceleration * from * from) / dt);
            }
            Check(h._motionSamples.Count == intervals.Length, "each unequal interval contributed one sample");
            double weighted = 0, weightTotal = 0, plain = 0;
            for (int i = 0; i < averages.Count; i++)
            {
                weighted += averages[i] * (i + 1);
                plain += averages[i];
                weightTotal += i + 1;
            }
            Vector3 l, a;
            Estimate(h, out l, out a);
            double expected = weighted / weightTotal, mean = plain / averages.Count;
            Near(Vector3.Dot(l, direction), expected, 5e-5, "weighted mean of interval averages along travel direction");
            Check(Math.Abs(Vector3.Dot(l, direction) - mean) > 1e-3,
                "estimate differs from the plain mean, so the newest sample is weighted highest");
            Check(Vector3.Dot(l, direction) > mean, "the newest-weighted estimate leads the plain mean while accelerating");

            var steady = Held();
            double travelled = 0;
            PoseFrame(steady, Vector3.zero, Quaternion.identity, 0.02f);
            for (int i = 0; i < 6; i++)
            {
                travelled += 0.25 * intervals[i];
                PoseFrame(steady, new Vector3((float)travelled, 0, 0), Quaternion.identity, (float)intervals[i]);
            }
            Estimate(steady, out l, out a);
            Vec(l, 0.25, 0, 0, 1e-5, "constant speed sampled on unequal dt still reports 0.25 m/s");
            Vec(a, 0, 0, 0, 0, "constant linear motion has zero angular estimate");
        }

        private static void TinyDeltaTime()
        {
            var h = Held();
            Vector3 start = new Vector3(1, 1, 1);
            PoseFrame(h, start, Quaternion.identity, 0.02f);
            foreach (float dt in new[] { 1e-5f, 1e-8f, 1e-15f, 0f, -0.001f, float.NaN })
            {
                int count = h._motionSamples.Count;
                PoseFrame(h, start + new Vector3(0.001f, 0, 0), Quaternion.identity, dt);
                Check(h._motionSamples.Count == count, "dt=" + dt + " stays below the sampling threshold");
                Vec(h._prevSamplePosition, start.x, start.y, start.z, 0, "dt=" + dt + " does not refresh the previous position");
            }

            // H1: the guard rejects dt <= 1e-4 but never refreshes _prevSample*, so displacement accumulated
            // during rejected frames is divided by the next accepted dt alone.
            var stale = Held();
            PoseFrame(stale, Vector3.zero, Quaternion.identity, 0.02f);
            for (int i = 1; i <= 100; i++)
                PoseFrame(stale, new Vector3(1e-5f * i, 0, 0), Quaternion.identity, 0f);
            Check(stale._motionSamples.Count == 0, "H1 dt=0 frames never sample");
            Vec(stale._prevSamplePosition, 0, 0, 0, 0, "H1 previous pose stays at the seed while the transform moved 1e-3 m");
            PoseFrame(stale, new Vector3(1.001e-3f, 0, 0), Quaternion.identity, 2e-4f);
            Check(stale._motionSamples.Count == 1, "H1 the first accepted frame produces one sample");
            Vector3 l, a;
            Estimate(stale, out l, out a);
            Near(l.x, 1.001e-3 / 2e-4, 1e-3, "H1 divides the whole stale displacement by the single accepted dt");
            Check(l.x > 4.0, "H1 reports " + l.x + " m/s for a frame whose own movement was zero");
            notes.Add("H1 UpdateMotionSampling returns without sampling when dt <= 1e-4, so _prevSamplePosition goes stale. "
                + "100 frames of 1e-5 m at dt=0 then one accepted dt=2e-4 frame reports "
                + l.x.ToString("F2") + " m/s although that frame itself moved 0 m. OnGrabEnd caps the consequence at "
                + "_maxLinearReleaseSpeed=" + stale._maxLinearReleaseSpeed + " m/s and _maxAngularReleaseSpeed="
                + stale._maxAngularReleaseSpeed + " rad/s, so the risk is a wrong-direction, saturated release rather than a runaway.");

            // H5: dt is never checked for finiteness, and +Infinity passes the threshold comparison.
            var inf = Held();
            PoseFrame(inf, Vector3.zero, Quaternion.identity, 0.02f);
            PoseFrame(inf, new Vector3(0.001f, 0, 0), Quaternion.identity, 0.05f);
            int before = inf._motionSamples.Count;
            PoseFrame(inf, new Vector3(0.002f, 0, 0), Quaternion.identity, float.PositiveInfinity);
            Check(inf._motionSamples.Count == before + 1, "H5 an infinite dt is accepted, not rejected");
            MotionSample poison = LastSample(inf);
            Check(float.IsPositiveInfinity(poison.DeltaTime), "H5 the infinite dt is stored in the queue");
            Vec(poison.LinearVelocity, 0, 0, 0, 0, "H5 the infinite-dt sample carries zero velocity");
            Estimate(inf, out l, out a);
            Check(l.x < 0.02f, "H5 the zero-velocity sample dilutes the weighted estimate to " + l.x + " m/s");
            notes.Add("H5 dt is compared but never validated as finite. A positive-infinity dt frame is sampled as a "
                + "zero-velocity sample and stays in the six-frame queue, under-estimating release velocity for up to "
                + "five subsequent frames.");
        }

        private static void LargeDeltaTime()
        {
            var h = Held();
            PoseFrame(h, Vector3.zero, Quaternion.identity, 0.016f);
            PoseFrame(h, new Vector3(0.0625f, 0, 0), Quaternion.AxisAngle(Vector3.up, 0.5f), 0.25f);
            Vector3 l, a;
            Estimate(h, out l, out a);
            Vec(l, 0.25, 0, 0, 1e-5, "large dt integrates the same linear speed");
            Vec(a, 0, 2, 0, 1e-3, "large dt integrates the same angular speed (0.5 rad / 0.25 s)");
            for (int i = 0; i < 10; i++)
                PoseFrame(h, new Vector3(0.0625f + 0.25f * 0.25f * (i + 1), 0, 0), Quaternion.identity, 0.25f);
            Estimate(h, out l, out a);
            Vec(l, 0.25, 0, 0, 1e-5, "repeated large dt stays consistent");
            Check(h._motionSamples.Count == 6, "large dt sampling remains queue bounded");
            foreach (MotionSample s in h._motionSamples) Near(s.DeltaTime, 0.25f, 1e-6, "stored dt matches the frame");

            // A single hitch frame keeps the geometric average, so a 0.25 s gap reports the gap speed.
            var hitch = Held();
            PoseFrame(hitch, Vector3.zero, Quaternion.identity, 0.02f);
            for (int i = 1; i <= 6; i++) PoseFrame(hitch, new Vector3(0.25f * 0.02f * i, 0, 0), Quaternion.identity, 0.02f);
            PoseFrame(hitch, new Vector3(0.25f * 0.12f + 0.5f, 0, 0), Quaternion.identity, 0.5f);
            Estimate(hitch, out l, out a);
            Check(l.x > 0.25f, "an oversized frame raises the estimate instead of being ignored");
            notes.Add("H2-adjacent: there is no maximum-dt gap rejection in sampling, so a frame hitch is treated as slow "
                + "continuous motion. Weighted averaging over six frames keeps the estimate finite and bounded by the release clamp.");
        }

        private static void SuddenReversal()
        {
            var h = Held();
            double t = 0;
            PoseFrame(h, Vector3.zero, Quaternion.identity, 0.02f);
            for (int i = 0; i < 6; i++)
            {
                t += 0.02;
                PoseFrame(h, new Vector3((float)(0.25 * t), 0, 0), Quaternion.identity, 0.02f);
            }
            Vector3 l, a;
            Estimate(h, out l, out a);
            Vec(l, 0.25, 0, 0, 1e-5, "steady forward motion before reversal");

            t += 0.02;
            float reversed = (float)(0.25 * t) - 0.01f;
            PoseFrame(h, new Vector3(reversed, 0, 0), Quaternion.identity, 0.02f);
            Estimate(h, out l, out a);
            Check(l.x > 0 && l.x < 0.25, "first reversed sample reduces the estimate but cannot erase history");

            for (int i = 0; i < 6; i++)
            {
                reversed -= 0.25f * 0.02f;
                PoseFrame(h, new Vector3(reversed, 0, 0), Quaternion.identity, 0.02f);
            }
            Estimate(h, out l, out a);
            Vec(l, -0.25, 0, 0, 1e-5, "six reversed samples fully evict the forward history");

            var violent = Held();
            PoseFrame(violent, Vector3.zero, Quaternion.identity, 0.02f);
            for (int i = 1; i <= 6; i++) PoseFrame(violent, new Vector3(0.5f * i, 0, 0), Quaternion.identity, 0.02f);
            PoseFrame(violent, new Vector3(-0.5f, 0, 0), Quaternion.identity, 0.02f);
            Estimate(violent, out l, out a);
            Check(l.x < -0.5f, "violent reversal estimate points backwards");
            violent.OnGrabEnd(new PointerEvent(Owner, PointerEventType.Unselect, new Pose()));
            Check(violent._rigidbody.linearVelocity.x < 0, "reversed direction survives into the release");
            Check(violent._rigidbody.linearVelocity.magnitude <= violent._maxLinearReleaseSpeed + 1e-6f,
                "violent reversal release is still clamped");
            Vec(violent._rigidbody.linearVelocity, -violent._maxLinearReleaseSpeed, 0, 0, 1e-5,
                "the clamp saturates along the reversed direction");
        }

        private static void QueueBoundary()
        {
            var h = Held();
            PoseFrame(h, Vector3.zero, Quaternion.identity, 1.0f);
            for (int k = 1; k <= 10; k++) PoseFrame(h, new Vector3(k * (k + 1) / 2f, 0, 0), Quaternion.identity, 1.0f);
            Check(h._motionSamples.Count == 6, "queue is capped at MaxMotionSamples=6");
            Check(h._motionSamples.Peek().LinearVelocity.x == 5f, "oldest retained sample is the 5th displacement");
            int index = 5;
            foreach (MotionSample s in h._motionSamples)
            {
                Near(s.LinearVelocity.x, index, 1e-6, "FIFO order from oldest to newest");
                Near(s.DeltaTime, 1.0f, 1e-6, "sample dt preserved");
                index++;
            }
            Check(index == 11, "exactly six samples enumerated");
            double weighted = 0, weightTotal = 0;
            for (int k = 5; k <= 10; k++) { weighted += k * (k - 4); weightTotal += k - 4; }
            Vector3 l, a;
            Estimate(h, out l, out a);
            Vec(l, weighted / weightTotal, 0, 0, 1e-5, "weighted mean covers only the six newest samples");
            Vec(a, 0, 0, 0, 0, "pure translation produces no angular history");
            Near(weightTotal, 21, 0, "weights are 1..6 oldest to newest");

            // Queue length must not grow when the same frame count is sampled through the Update entry point.
            var via = Held();
            PoseFrame(via, Vector3.zero, Quaternion.identity, 0.02f);
            for (int i = 0; i < 50; i++)
            {
                via._grabbable.SelectingPoints.Add(new Pose());
                via.transform.position = new Vector3(0.005f * i, 0, 0);
                via.Update();
            }
            Check(via._motionSamples.Count == 6, "Update-driven sampling stays bounded at six samples");
            Estimate(via, out l, out a);
            Vec(l, 0.25, 0, 0, 1e-5, "steady Update-driven sampling reports the true speed");
        }

        private static void QuaternionWrap()
        {
            var axis = Axis(0.3f, 1, -0.7f);
            const double omega = 1.0;
            var h = Held();
            double t = 0;
            PoseFrame(h, Vector3.zero, Quaternion.AxisAngle(axis, 0), 0.02f);
            bool crossedAbsolute180 = false;
            for (int i = 0; i < 400; i++)
            {
                t += 0.02;
                double angle = omega * t;
                if (angle > Math.PI) crossedAbsolute180 = true;
                PoseFrame(h, Vector3.zero, Quaternion.AxisAngle(axis, (float)angle), 0.02f);
                Vector3 l, a;
                Estimate(h, out l, out a);
                if (i > 10)
                {
                    Vec(a, axis.x * omega, axis.y * omega, axis.z * omega, 2e-3, "absolute orientation crossing 180 degrees stays continuous");
                    Check(Math.Abs(Vector3.Dot(a, axis) - omega) < 2e-3, "no per-frame sign flip while spinning past 180 degrees");
                }
            }
            Check(crossedAbsolute180, "test really crossed the absolute 180 degree orientation");

            foreach (double degrees in new[] { 179.0, 181.0, 180.0, 359.0, 360.0, 361.0, 540.0, 719.0, 720.0, 721.0 })
            {
                var probe = Held();
                Time.deltaTime = 1.0f;
                probe.transform.position = Vector3.zero;
                probe.transform.rotation = Quaternion.AxisAngle(axis, 0);
                probe.UpdateMotionSampling();
                probe.transform.rotation = Quaternion.AxisAngle(axis, (float)(degrees * Math.PI / 180));
                probe.UpdateMotionSampling();
                double signed = Vector3.Dot(LastSample(probe).AngularVelocity, axis);
                notes.Add("H2 single-frame relative rotation of " + degrees.ToString("F0")
                    + " degrees is reported as " + signed.ToString("F4") + " rad/s");
                Check(Math.Abs(signed) <= Math.PI + 1e-3, degrees + " degree fold stays within one half turn");
                if (Math.Abs(degrees % 360) < 0.001 || Math.Abs(degrees % 360 - 360) < 0.001)
                    Check(Math.Abs(signed) < 1e-3, degrees + " degree full turn folds to zero");
                else if (Math.Abs(degrees - 180) < 0.001 || Math.Abs(degrees - 540) < 0.001)
                    Check(Math.Abs(Math.Abs(signed) - Math.PI) < 1e-3, degrees + " degree half turn folds to magnitude pi");
                else if (degrees == 179)
                    Check(signed > 0 && Math.Abs(signed - 179 * Math.PI / 180) < 1e-3, "179 degree step keeps its direction");
                else if (degrees == 181)
                    Check(signed < 0 && Math.Abs(signed + 179 * Math.PI / 180) < 1e-3, "181 degree step flips direction");
                else
                    Check(Math.Abs(Math.Abs(signed) - (360 - degrees % 360) * Math.PI / 180) < 1e-2
                        || Math.Abs(Math.Abs(signed) - (degrees % 360) * Math.PI / 180) < 1e-2, degrees + " degree alias magnitude");
            }
            notes.Add("H2 production folds a single >180 degree result by subtracting 360, so any per-frame relative "
                + "rotation above 180 degrees is indistinguishable from slower or opposite motion. The 181 degree step "
                + "above is reported as -179 degrees: direction reversed. Only a tracking teleport or a long hitch can "
                + "produce that at grab speeds, and the release clamps bound the damage.");
        }

        private static void ReleaseClamp()
        {
            var axis = Axis(1, 1, 1);
            foreach (ReleaseProfile profile in new[] { ReleaseProfile.Freeze, ReleaseProfile.ResidualSettle })
            {
                var h = Held(profile);
                PoseFrame(h, Vector3.zero, Quaternion.identity, 0.02f);
                for (int i = 1; i <= 6; i++)
                    PoseFrame(h, new Vector3(5f * i, 0, 0), Quaternion.AxisAngle(axis, (float)(40 * 0.02 * i)), 0.02f);
                h.OnGrabEnd(new PointerEvent(Owner, PointerEventType.Unselect, new Pose()));
                Vector3 lin = h._rigidbody.linearVelocity, ang = h._rigidbody.angularVelocity;
                if (profile == ReleaseProfile.Freeze)
                {
                    Check(h._rigidbody.isKinematic, "Freeze keeps the body kinematic");
                    Vec(lin, 0, 0, 0, 0, "Freeze release linear velocity");
                    Vec(ang, 0, 0, 0, 0, "Freeze release angular velocity");
                    Check(!Audit.Any(x => x == "kinematic:False"), "Freeze never requests dynamic");
                    Check(h._state == FingerPlayState.Settled, "Freeze releases straight to Settled");
                    continue;
                }
                Check(Finite(lin) && Finite(ang), "released velocities are finite");
                Vec(lin, h._maxLinearReleaseSpeed, 0, 0, 1e-5, "over-limit linear clamps to the configured cap");
                Vec(ang, axis.x * h._maxAngularReleaseSpeed, axis.y * h._maxAngularReleaseSpeed,
                    axis.z * h._maxAngularReleaseSpeed, 1e-5, "over-limit angular clamps along the spin axis");
                Check(!h._rigidbody.isKinematic, "ResidualSettle switches to dynamic");
                Check(!h._rigidbody.useGravity, "release disables gravity");
                Near(h._rigidbody.linearDamping, h._linearDamping, 0, "configured linear damping applied");
                Near(h._rigidbody.angularDamping, h._angularDamping, 0, "configured angular damping applied");
                Check(h._settleTimer == 0 && h._settleHoldTimer == 0, "release resets settle timers");
                Check(h._state == FingerPlayState.Settling, "release enters Settling");
                Check(h._currentOwnerId == -1 && h._activeHand == "None", "release exits interaction authority");
                int toDynamic = Audit.Events.FindIndex(x => x == "kinematic:False");
                int firstWrite = Audit.Events.FindIndex(x => x.StartsWith("linear:") || x.StartsWith("angular:"));
                Check(toDynamic >= 0 && firstWrite > toDynamic, "dynamic transition precedes the velocity write");

                // Settle thresholds and caps are the only thing that returns the body to kinematic.
                Time.fixedDeltaTime = 0.02f;
                for (int i = 0; i < 5; i++) h.FixedUpdate();
                Check(h._state == FingerPlayState.Settling, "hold time below the settle hold window keeps settling");
                Near(h._settleTimer, 0.1, 1e-6, "settle timer accumulates fixedDeltaTime");
                h._rigidbody.linearVelocity = Vector3.zero;
                h._rigidbody.angularVelocity = Vector3.zero;
                for (int i = 0; i < 100; i++) h.FixedUpdate();
                Check(h._state == FingerPlayState.Settled, "threshold hold completes settling");
                Check(h._rigidbody.isKinematic, "settling relocks the body kinematic");
                Vec(h._rigidbody.linearVelocity, 0, 0, 0, 0, "settling zeroes linear velocity");
                Vec(h._rigidbody.angularVelocity, 0, 0, 0, 0, "settling zeroes angular velocity");
                Check(Audit.Any(x => x.Contains("SETTLE_END")), "settling logs its end");
            }

            var mid = Held();
            PoseFrame(mid, Vector3.zero, Quaternion.identity, 0.02f);
            for (int i = 1; i <= 6; i++) PoseFrame(mid, new Vector3(0.002f * i, -0.001f * i, 0f), Quaternion.identity, 0.02f);
            mid.OnGrabEnd(new PointerEvent(Owner, PointerEventType.Unselect, new Pose()));
            Vec(mid._rigidbody.linearVelocity, 0.02, -0.01, 0, 1e-6, "mid-range release equals estimate times the 0.2 scale");

            var zero = Held();
            zero._linearReleaseScale = 0f;
            zero._angularReleaseScale = 0f;
            PoseFrame(zero, Vector3.zero, Quaternion.identity, 0.02f);
            for (int i = 1; i <= 6; i++) PoseFrame(zero, new Vector3(0.5f * i, 0, 0), Quaternion.identity, 0.02f);
            zero.OnGrabEnd(new PointerEvent(Owner, PointerEventType.Unselect, new Pose()));
            Vec(zero._rigidbody.linearVelocity, 0, 0, 0, 0, "zero scale inherits no linear velocity");
            Vec(zero._rigidbody.angularVelocity, 0, 0, 0, 0, "zero scale inherits no angular velocity");

            var under = Held();
            under._linearReleaseScale = 0.5f;
            under._maxLinearReleaseSpeed = 0.4f;
            PoseFrame(under, Vector3.zero, Quaternion.identity, 0.02f);
            for (int i = 1; i <= 6; i++) PoseFrame(under, new Vector3(0.16f * i, 0, 0), Quaternion.identity, 0.02f);
            under.OnGrabEnd(new PointerEvent(Owner, PointerEventType.Unselect, new Pose()));
            Vec(under._rigidbody.linearVelocity, 0.4, 0, 0, 1e-6, "exactly at the cap is not reduced further");

            var locked = Held();
            locked._selectionRecoveryLocked = true;
            PoseFrame(locked, Vector3.zero, Quaternion.identity, 0.02f);
            for (int i = 1; i <= 6; i++) PoseFrame(locked, new Vector3(i, 0, 0), Quaternion.identity, 0.02f);
            locked.OnGrabEnd(new PointerEvent(Owner, PointerEventType.Unselect, new Pose()));
            Check(locked._rigidbody.isKinematic && locked._state == FingerPlayState.Settled, "recovery lock bypasses release physics");
            Vec(locked._rigidbody.linearVelocity, 0, 0, 0, 0, "recovery lock writes no linear velocity");
            Vec(locked._rigidbody.angularVelocity, 0, 0, 0, 0, "recovery lock writes no angular velocity");
            Check(Audit.WarningLogs == 1, "recovery lock release logs exactly one warning");
        }

        private static void NonFiniteInput()
        {
            foreach (Vector3 bad in new[]
            {
                new Vector3(float.NaN, 0, 0),
                new Vector3(0, float.PositiveInfinity, 0),
                new Vector3(float.NegativeInfinity, 1, 1)
            })
            {
                var h = Held();
                PoseFrame(h, Vector3.zero, Quaternion.identity, 0.02f);
                PoseFrame(h, bad, Quaternion.identity, 0.02f);
                Vector3 l, a;
                h.GetEstimatedVelocities(out l, out a);
                bool estimateFinite = Finite(l) && Finite(a);
                notes.Add("H3 non-finite position " + bad + " gave linear " + l + " and angular " + a);
                if (estimateFinite)
                {
                    notes.Add("H3 configuration masked non-finite input before the estimate; verifying release anyway");
                    continue;
                }
                h.OnGrabEnd(new PointerEvent(Owner, PointerEventType.Unselect, new Pose()));
                Check(!Finite(h._rigidbody.linearVelocity),
                    "H3 confirmed: the non-finite estimate passes scaling and clamping into the Rigidbody write");
                Check(Audit.Any(x => x == "NON-FINITE-VELOCITY-WRITE"), "H3 non-finite write recorded");
            }
            notes.Add("H3 OnGrabEnd has no finite guard: Vector3.ClampMagnitude and the manual angular guard both let "
                + "NaN/Infinity through because NaN comparisons are false, so a non-finite transform reaches Rigidbody "
                + "velocity. Reported as a production hazard; not fixed in this round.");

            var angular = Held();
            PoseFrame(angular, Vector3.zero, new Quaternion(float.NaN, 0, 0, 1), 0.02f);
            PoseFrame(angular, Vector3.zero, new Quaternion(float.NaN, 0f, 0.5f, 0.5f), 0.02f);
            Vector3 al, aa;
            angular.GetEstimatedVelocities(out al, out aa);
            Check(!float.IsNaN(aa.x) && !float.IsNaN(aa.y) && !float.IsNaN(aa.z),
                "NaN rotation cannot yield a NaN angular sample because the axis guard rejects it");
            notes.Add("H3 asymmetry: the angular path is guarded by `axis.sqrMagnitude > 1e-6f && !float.IsNaN(axis.x)` "
                + "(the magnitude test alone already rejects a NaN axis), while the linear path has no equivalent guard.");

            var afterBad = Held();
            Vector3 rl, ra;
            PoseFrame(afterBad, Vector3.zero, Quaternion.identity, 0.02f);
            PoseFrame(afterBad, new Vector3(float.NaN, 0, 0), Quaternion.identity, 0.02f);
            Check(!Finite(LastSample(afterBad).LinearVelocity), "H4 the non-finite frame itself samples as non-finite");
            PoseFrame(afterBad, new Vector3(0.005f, 0, 0), Quaternion.identity, 0.02f);
            Check(!Finite(LastSample(afterBad).LinearVelocity),
                "H4 the first clean frame is still poisoned because _prevSamplePosition held the bad pose");
            PoseFrame(afterBad, new Vector3(0.01f, 0, 0), Quaternion.identity, 0.02f);
            Check(Finite(LastSample(afterBad).LinearVelocity), "H4 sampling self-heals once the previous pose is finite");
            afterBad.GetEstimatedVelocities(out rl, out ra);
            Check(!Finite(rl), "H4 the weighted estimate stays non-finite while either poisoned sample is queued");
            for (int i = 4; i <= 10; i++) PoseFrame(afterBad, new Vector3(0.005f * i, 0, 0), Quaternion.identity, 0.02f);
            afterBad.GetEstimatedVelocities(out rl, out ra);
            notes.Add("H4 poisoning: one non-finite frame poisons two samples; FIFO eviction clears them without a reset "
                + "(estimate after ten clean frames = " + rl + ")");
            Check(Finite(rl), "H4 after six further samples the poisoned entries are evicted and the estimate recovers");
            Check(Math.Abs(rl.x - 0.25) < 1e-5, "recovered estimate reports the true speed again");

            afterBad.OnGrabBegin(new PointerEvent(Owner, PointerEventType.Select, new Pose()));
            Check(afterBad._motionSamples.Count == 0 && !afterBad._hasPrevSample, "a fresh grab reseeds the sampler");
            PoseFrame(afterBad, new Vector3(0.25f * 0.02f, 0, 0), Quaternion.identity, 0.02f);
            Check(afterBad._motionSamples.Count == 0, "the frame after a grab only seeds");
            PoseFrame(afterBad, new Vector3(0.25f * 0.04f, 0, 0), Quaternion.identity, 0.02f);
            afterBad.GetEstimatedVelocities(out rl, out ra);
            Check(Finite(rl), "after reseeding, sampling is finite again");
            Vec(rl, 0.25, 0, 0, 1e-5, "post-reseed estimate reports the true speed");

            // A grab that seeds a non-finite transform costs one sample, then recovers; a transform that stays
            // non-finite (tracking lost) keeps every sample and the release write non-finite for the whole grab.
            var badSeed = Held();
            PoseFrame(badSeed, new Vector3(float.PositiveInfinity, 0, 0), Quaternion.identity, 0.02f);
            Check(badSeed._hasPrevSample && !Finite(badSeed._prevSamplePosition), "H4 a non-finite pose can become the seed");
            PoseFrame(badSeed, new Vector3(0.005f, 0, 0), Quaternion.identity, 0.02f);
            Check(!Finite(LastSample(badSeed).LinearVelocity), "H4 the first frame after a bad seed is non-finite");
            PoseFrame(badSeed, new Vector3(0.01f, 0, 0), Quaternion.identity, 0.02f);
            Check(Finite(LastSample(badSeed).LinearVelocity), "H4 a bad seed costs exactly one sample, then sampling recovers");

            var stuck = Held();
            PoseFrame(stuck, Vector3.zero, Quaternion.identity, 0.02f);
            for (int i = 1; i <= 10; i++) PoseFrame(stuck, new Vector3(float.NaN, 0, 0), Quaternion.identity, 0.02f);
            stuck.GetEstimatedVelocities(out rl, out ra);
            Check(!Finite(rl), "H4 confirmed: while the transform stays non-finite every sample and the estimate do too");
            stuck.OnGrabEnd(new PointerEvent(Owner, PointerEventType.Unselect, new Pose()));
            Check(!Finite(stuck._rigidbody.linearVelocity), "H4 a stuck non-finite transform reaches the Rigidbody on release");
            notes.Add("H4 lifetime: a single non-finite frame costs at most two poisoned samples and FIFO eviction clears "
                + "them within six frames, so a finite transform always self-heals. There is no finite-value guard, so a "
                + "transform that stays non-finite releases with a non-finite Rigidbody velocity.");
        }

        private static void HistoryContamination()
        {
            var dirty = Held();
            for (int i = 0; i <= 6; i++)
                PoseFrame(dirty, new Vector3(0.4f * i, 0, 0), Quaternion.AxisAngle(Vector3.up, i * 0.6f), 0.02f);
            dirty._settleTimer = 0.31f;
            dirty._settleHoldTimer = 0.09f;
            Check(dirty._motionSamples.Count == 6 && dirty._hasPrevSample, "history is dirty before cancellation");

            dirty.OnGrabCancel(new PointerEvent(Owner, PointerEventType.Cancel, new Pose()));
            Check(dirty._motionSamples.Count == 0, "Cancel clears the sampling queue");
            Check(!dirty._hasPrevSample, "Cancel clears the previous pose flag");
            Check(dirty._settleTimer == 0 && dirty._settleHoldTimer == 0, "Cancel clears both settle timers");
            Check(dirty._rigidbody.isKinematic && dirty._state == FingerPlayState.Settled, "Cancel freezes safely");
            Vec(dirty._rigidbody.linearVelocity, 0, 0, 0, 0, "Cancel leaves no residual linear velocity");
            Vec(dirty._rigidbody.angularVelocity, 0, 0, 0, 0, "Cancel leaves no residual angular velocity");
            Check(!Audit.Any(x => x.Contains("SETTLE_BEGIN") || x.Contains("FINGER_PLAY_RELEASE")),
                "Cancel never enters the release path");
            Check(!Audit.Any(x => x == "kinematic:False"), "Cancel never requests dynamic");

            Time.deltaTime = 0.02f;
            dirty.transform.position = new Vector3(50, 50, 50);
            dirty.Update();
            Check(dirty._motionSamples.Count == 0 && !dirty._hasPrevSample, "Update after Cancel does not resample");

            var fresh = Held();
            dirty.OnGrabBegin(new PointerEvent(Owner, PointerEventType.Select, new Pose()));
            Check(dirty._state == FingerPlayState.Grabbed, "regrab after Cancel is accepted");
            // The 10 m teleport is intentional: it models the coin being grabbed elsewhere after a lost selection.
            PoseFrame(dirty, new Vector3(10, 0, 0), Quaternion.identity, 0.02f);
            PoseFrame(fresh, Vector3.zero, Quaternion.identity, 0.02f);
            double t = 0;
            for (int cycle = 0; cycle < 6; cycle++)
            {
                t += 0.02;
                PoseFrame(dirty, new Vector3(10 + 0.25f * (float)t, 0, 0), Quaternion.AxisAngle(Vector3.up, (float)t), 0.02f);
                PoseFrame(fresh, new Vector3(0.25f * (float)t, 0, 0), Quaternion.AxisAngle(Vector3.up, (float)t), 0.02f);
            }
            Vector3 dl, da, fl, fa;
            Estimate(dirty, out dl, out da);
            Estimate(fresh, out fl, out fa);
            // Absolute world coordinates quantise float deltas, so equality is per-ulp, not bitwise.
            Vec(dl, fl.x, fl.y, fl.z, 2e-4, "post-Cancel grab reproduces a fresh grab within float rounding");
            Vec(da, fa.x, fa.y, fa.z, 2e-4, "post-Cancel angular history reproduces a fresh grab within float rounding");
            dirty.OnGrabEnd(new PointerEvent(Owner, PointerEventType.Unselect, new Pose()));
            fresh.OnGrabEnd(new PointerEvent(Owner, PointerEventType.Unselect, new Pose()));
            Vec(dirty._rigidbody.linearVelocity, fresh._rigidbody.linearVelocity.x, fresh._rigidbody.linearVelocity.y,
                fresh._rigidbody.linearVelocity.z, 2e-4, "post-Cancel release velocity matches a fresh release");
            Vec(dirty._rigidbody.angularVelocity, fresh._rigidbody.angularVelocity.x, fresh._rigidbody.angularVelocity.y,
                fresh._rigidbody.angularVelocity.z, 2e-4, "post-Cancel release angular velocity matches a fresh release");
            notes.Add("Sampling at a 10 m world offset differs from the same motion at the origin by up to "
                + Math.Abs(dl.x - fl.x).ToString("R") + " m/s because float ulp grows with coordinates; the coin sits "
                + "within arm's reach, so this is a precision note rather than a hazard.");

            var settling = Held();
            PoseFrame(settling, Vector3.zero, Quaternion.identity, 0.02f);
            for (int i = 1; i <= 6; i++) PoseFrame(settling, new Vector3(0.2f * i, 0, 0), Quaternion.identity, 0.02f);
            settling.OnGrabEnd(new PointerEvent(Owner, PointerEventType.Unselect, new Pose()));
            Check(settling._state == FingerPlayState.Settling, "release enters settling");
            Time.fixedDeltaTime = 0.02f;
            for (int i = 0; i < 40; i++) settling.FixedUpdate();
            Check(settling._state == FingerPlayState.Settled, "duration cap completes settling without engine physics");
            Check(settling._settleTimer == 0 && settling._settleHoldTimer == 0, "settle completion resets both timers");
            Check(settling._rigidbody.isKinematic, "settle completion relocks the body");
            Vec(settling._rigidbody.linearVelocity, 0, 0, 0, 0, "settle completion zeroes velocity before relocking");
            settling.OnGrabBegin(new PointerEvent(Owner, PointerEventType.Select, new Pose()));
            Check(settling._motionSamples.Count == 0 && !settling._hasPrevSample, "grab after settling restarts sampling empty");
            Vec(settling._rigidbody.linearVelocity, 0, 0, 0, 0, "grab after settling keeps zeroed velocity");

            var regrabDuringSettling = Held();
            PoseFrame(regrabDuringSettling, Vector3.zero, Quaternion.identity, 0.02f);
            for (int i = 1; i <= 6; i++) PoseFrame(regrabDuringSettling, new Vector3(0.2f * i, 0, 0), Quaternion.identity, 0.02f);
            regrabDuringSettling.OnGrabEnd(new PointerEvent(Owner, PointerEventType.Unselect, new Pose()));
            PoseFrame(regrabDuringSettling, new Vector3(5, 0, 0), Quaternion.identity, 0.02f);
            regrabDuringSettling.OnGrabBegin(new PointerEvent(Owner, PointerEventType.Select, new Pose()));
            Check(regrabDuringSettling._motionSamples.Count == 0 && !regrabDuringSettling._hasPrevSample
                  && regrabDuringSettling._settleTimer == 0 && regrabDuringSettling._settleHoldTimer == 0,
                "regrab during settling clears sampling and settle timers");
            Check(regrabDuringSettling._rigidbody.isKinematic, "regrab during settling restores kinematic authority");
            Vec(regrabDuringSettling._rigidbody.linearVelocity, 0, 0, 0, 0, "regrab during settling zeroes velocity");
        }
    }
}
