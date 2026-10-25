// P2 FLIGHT DYNAMICS ISOLATED | NOT INTEGRATED | NOT RUNTIME VALIDATED | NO RNG / NO RESULT / NO CATCH
using System;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Toss.Experiments.Flight
{
    public static class FlightDynamicsTests
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
        private static void Near(double actual, double expected, double tolerance, string why)
            => Check(Math.Abs(actual - expected) <= tolerance, why + " actual=" + actual.ToString("R", CultureInfo.InvariantCulture));

        private static FlightLimits Wide(double time = 2, double step = 0.25, double speed = 20)
            => new FlightLimits(new Vector3(-10), new Vector3(10), time, step, speed, 30, 100);
        private static LaunchInput Input(long id = 1, long session = 1, Vector3? position = null, Vector3? velocity = null,
            Vector3? gravity = null, Vector3? angular = null, Quaternion? orientation = null)
            => new LaunchInput(new IntentIdentity(session, id), position ?? new Vector3(0, 1, 0.5f), orientation ?? Quaternion.Identity,
                velocity ?? new Vector3(0, 3, 0), angular ?? Vector3.Zero, gravity ?? new Vector3(0, -8, 0));
        private static FlightState Record(FlightState s)
        {
            foreach (double x in new[] { s.Position.X, s.Position.Y, s.Position.Z, s.Orientation.X, s.Orientation.Y, s.Orientation.Z,
                s.Orientation.W, s.LinearVelocity.X, s.LinearVelocity.Y, s.LinearVelocity.Z, s.AngularVelocity.X,
                s.AngularVelocity.Y, s.AngularVelocity.Z, s.Elapsed })
            {
                if (!Numbers.Finite(x)) throw new Exception("Non-finite proposal");
                Trace.Append(x.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            }
            Trace.Append(s.Status).Append('|').Append(s.Phase).Append('|').Append(s.StopReason).Append('|')
                .Append(s.Intent.Session).Append('|').Append(s.Intent.Sequence).Append('|').Append(s.RequestReason).AppendLine();
            return s;
        }
        private static FlightState To(FlightDynamics d, double target, double rate = 72)
        {
            int steps = 0;
            while (d.Current.Status == FlightStatus.Running && target > d.Current.Elapsed)
            {
                var s = Record(d.Step(Math.Min(1 / rate, target - d.Current.Elapsed)));
                if (!s.RequestAccepted) throw new Exception("Unexpected rejection " + s.RequestReason);
                if (!d.Limits.Contains(s.Position)) throw new Exception("Proposal outside spatial bounds");
                if (++steps > 10000) throw new Exception("Trajectory failed to stop");
            }
            return d.Current;
        }

        public static int Main()
        {
            Case("1. Zero launch speed falls under gravity; zero gravity coasts", () =>
            {
                var d = new FlightDynamics(Wide());
                Record(d.Launch(Input(velocity: Vector3.Zero)));
                var s = To(d, 0.25);
                Near(s.Position.Y, 0.75, 1e-6, "zero-speed ballistic position");
                Near(s.LinearVelocity.Y, -2, 1e-6, "gravity changes velocity");
                Check(s.Phase == FlightPhase.Falling, "zero-speed launch must descend");
                var free = new FlightDynamics(Wide());
                free.Launch(Input(velocity: Vector3.Zero, gravity: Vector3.Zero));
                var still = To(free, 1);
                Check(still.Position == new Vector3(0, 1, 0.5f) && still.Phase == FlightPhase.Coasting, "zero force and speed stay fixed");
            });
            Case("2. Upward launch, exact apex and descent agree with position derivative", () =>
            {
                var d = new FlightDynamics(Wide());
                Check(Record(d.Launch(Input())).Phase == FlightPhase.Rising, "positive up speed rises");
                var a = To(d, 0.25); var apex = To(d, 0.375); var b = To(d, 0.75);
                Check(apex.Phase == FlightPhase.Apex && b.Phase == FlightPhase.Falling, "apex and descent identified");
                Near(apex.Position.Y, 1.5625, 1e-6, "apex height = v0^2/(2g)");
                Near(apex.LinearVelocity.Y, 0, 1e-6, "vertical apex velocity");
                Near(b.Position.Y, 1, 1e-6, "returns to launch height");
                Near(b.Position.Y - a.Position.Y, (a.LinearVelocity.Y + b.LinearVelocity.Y) * 0.5 * (b.Elapsed - a.Elapsed),
                    1e-6, "displacement matches integrated linear velocity");
            });
            Case("3. Common times agree at 30/60/72/90/120 Hz and variable dt", () =>
            {
                foreach (double rate in new[] { 30.0, 60, 72, 90, 120 })
                {
                    var d = new FlightDynamics(Wide()); d.Launch(Input(velocity: new Vector3(0.2f, 3, 0.1f)));
                    var s = To(d, 0.75, rate);
                    Near(s.Position.X, 0.15, 1e-6, rate + " Hz horizontal position");
                    Near(s.Position.Y, 1, 1e-6, rate + " Hz vertical position");
                    Near(s.LinearVelocity.Y, -3, 1e-6, rate + " Hz velocity");
                }
                var variable = new FlightDynamics(Wide()); variable.Launch(Input());
                foreach (double dt in new[] { 0.017, 0.023, 0.011, 0.249, 0.20, 0.25 }) Record(variable.Step(dt));
                Near(variable.Current.Position.Y, 1, 1e-6, "variable-step position at 0.75 s");
            });
            Case("4. Explicit Euler has timestep error; absolute-time evaluation does not accumulate it", () =>
            {
                double previousError = double.PositiveInfinity;
                foreach (int rate in new[] { 30, 120 })
                {
                    double p = 1, v = 3, dt = 1.0 / rate;
                    for (int i = 0; i < rate; i++) { p += v * dt; v -= 8 * dt; }
                    double error = Math.Abs(p);
                    Check(error < previousError && error > 0.01, "Euler error shrinks with dt but remains nonzero");
                    previousError = error;
                    var exact = new FlightDynamics(Wide()); exact.Launch(Input());
                    Near(To(exact, 1, rate).Position.Y, 0, 1e-6, "analytic trajectory at same final time");
                }
            });
            Case("5. Non-finite launch and degenerate quaternions are rejected without consuming identity", () =>
            {
                foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    foreach (var input in new[] { Input(position: new Vector3(bad, 1, 0)), Input(velocity: new Vector3(0, bad, 0)),
                        Input(gravity: new Vector3(0, bad, 0)), Input(angular: new Vector3(bad, 0, 0)),
                        Input(orientation: new Quaternion(bad, 0, 0, 1)) })
                    {
                        var d = new FlightDynamics(Wide()); var r = Record(d.Launch(input));
                        Check(!r.RequestAccepted && r.Status == FlightStatus.Idle, "invalid launch leaves idle state");
                        Check(d.Launch(Input()).RequestAccepted, "rejected identity was not consumed");
                    }
                }
                foreach (var q in new[] { new Quaternion(0, 0, 0, 0), new Quaternion(0, 0, 0, 1e-20f) })
                    Check(!new FlightDynamics(Wide()).Launch(Input(orientation: q)).RequestAccepted, "degenerate quaternion rejected");
                var normalized = new FlightDynamics(Wide()).Launch(Input(orientation: new Quaternion(0, 0, 0, float.MaxValue)));
                Check(normalized.RequestAccepted && normalized.Orientation == Quaternion.Identity, "large finite quaternion normalized without float overflow");
                bool invalidLimit = false;
                try { new FlightLimits(Vector3.Zero, Vector3.One, maxDuration: double.NaN); }
                catch (ArgumentOutOfRangeException) { invalidLimit = true; }
                Check(invalidLimit, "non-finite limits rejected");
            });
            Case("6. Zero, negative and non-finite dt do not move; a large gap terminates at last safe state", () =>
            {
                var d = new FlightDynamics(Wide()); d.Launch(Input()); To(d, 0.1);
                var before = d.Current;
                foreach (double dt in new[] { 0.0, -0.01, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                {
                    var s = Record(d.Step(dt));
                    Check(!s.RequestAccepted && s.RequestReason == RequestReason.InvalidDeltaTime && s.Position == before.Position
                        && s.Elapsed == before.Elapsed, "invalid dt is a rejected no-op");
                }
                var gap = Record(d.Step(double.MaxValue));
                Check(gap.Status == FlightStatus.Stopped && gap.StopReason == StopReason.TimeGap && gap.Elapsed == before.Elapsed,
                    "no catch-up displacement on a time gap");
                Check(!d.Step(0.01).RequestAccepted, "stopped trajectory cannot resume without a new launch");
            });
            Case("7. Continuous bounds catch an exit and reentry inside a single step", () =>
            {
                var limits = new FlightLimits(new Vector3(-1), new Vector3(1, 0.2f, 1), 2, 0.25, 20, 30, 100);
                var d = new FlightDynamics(limits);
                d.Launch(Input(position: Vector3.Zero, velocity: new Vector3(0, 4, 0), gravity: new Vector3(0, -32, 0)));
                var s = Record(d.Step(0.25));
                Check(s.StopReason == StopReason.SpatialBoundary && s.Elapsed < 0.125, "stops before apex, even though endpoint y(0.25)=0 is inside");
                Near(s.Position.Y, 0.2, 1e-6, "ceiling contact position");
                Check(limits.Contains(s.Position), "terminal position is inside the closed envelope");

                var edgeLimits = new FlightLimits(Vector3.Zero, Vector3.One, 2, 0.25, 20, 30, 100);
                var outward = new FlightDynamics(edgeLimits).Launch(Input(position: Vector3.Zero, velocity: -Vector3.UnitX, gravity: Vector3.Zero));
                Check(outward.Status == FlightStatus.Stopped && outward.Elapsed == 0, "outward launch at boundary stops at t=0");
                var inward = new FlightDynamics(edgeLimits); inward.Launch(Input(position: Vector3.Zero, velocity: Vector3.UnitX, gravity: Vector3.Zero));
                Check(To(inward, 0.25).Status == FlightStatus.Running, "inward launch at boundary is legal");
                var tangent = new FlightDynamics(new FlightLimits(new Vector3(-1), new Vector3(1, 0.25f, 1), 2, 0.25, 20, 30, 100));
                tangent.Launch(Input(position: Vector3.Zero, velocity: new Vector3(0, 2, 0)));
                Near(To(tangent, 0.5).Elapsed, 0.25, 1e-9, "tangent contact conservatively terminates");
            });
            Case("8. Time and speed are finite terminal limits, not recovery actions", () =>
            {
                var timed = new FlightDynamics(Wide(time: 0.375)); timed.Launch(Input());
                var t = To(timed, 1);
                Check(t.StopReason == StopReason.TimeLimit && t.Elapsed == 0.375, "clamped to exact lifetime");
                var speed = new FlightDynamics(Wide(speed: 2)); speed.Launch(Input(velocity: Vector3.Zero));
                var s = To(speed, 0.5);
                Check(s.StopReason == StopReason.SpeedLimit, "gravity cannot accelerate beyond the configured speed boundary");
                Near(s.Elapsed, 0.25, 1e-9, "time to speed limit");
                Near(s.LinearVelocity.Y, -2, 1e-6, "terminal velocity is diagnostic, not forced to zero");
                Check(!new FlightDynamics(Wide(speed: 2)).Launch(Input()).RequestAccepted, "excessive initial speed rejected, not remapped");
                Check(!new FlightDynamics(Wide()).Launch(Input(angular: new Vector3(31, 0, 0))).RequestAccepted, "angular speed cap applies at launch");
            });
            Case("9. Duplicate intents stay consumed across Reset; a new identity can launch", () =>
            {
                var d = new FlightDynamics(Wide());
                Check(!d.Launch(Input(id: 0)).RequestAccepted, "zero identity invalid");
                Check(d.Launch(Input()).RequestAccepted, "first launch accepted");
                Check(d.Launch(Input()).RequestReason == RequestReason.DuplicateIntent, "same running intent is not launched twice");
                Check(d.Launch(Input(id: 2)).RequestReason == RequestReason.Busy, "another intent cannot replace active flight");
                var reset = Record(d.Reset());
                Check(reset.Status == FlightStatus.Idle && reset.Elapsed == 0 && !reset.Intent.IsValid && reset.LinearVelocity == Vector3.Zero
                    && reset.AngularVelocity == Vector3.Zero && reset.Orientation == Quaternion.Identity, "Reset clears motion and active identity");
                Check(d.Launch(Input()).RequestReason == RequestReason.DuplicateIntent, "Reset does not reopen replay of a consumed intent");
                Check(d.Launch(Input(id: 2)).RequestAccepted, "previously busy identity was not consumed");
                d.Reset();
                Check(d.Launch(Input(session: 2)).RequestAccepted, "session namespace distinguishes sequence 1");
            });
            Case("10. Rotation uses world angular velocity, unit quaternions and continuous signs", () =>
            {
                var q0 = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)(Math.PI / 2));
                var d = new FlightDynamics(Wide());
                d.Launch(Input(velocity: Vector3.Zero, gravity: Vector3.Zero, orientation: q0, angular: new Vector3((float)(2 * Math.PI), 0, 0)));
                var quarter = To(d, 0.25);
                Check(Vector3.Distance(Vector3.Transform(Vector3.UnitX, quarter.Orientation), Vector3.UnitZ) < 1e-6,
                    "world X rotation is composed after initial Z orientation");
                var previous = quarter.Orientation;
                for (int i = 0; i < 54; i++)
                {
                    var s = Record(d.Step(1.0 / 72));
                    Near(s.Orientation.Length(), 1, 1e-6, "unit quaternion");
                    Check(Quaternion.Dot(previous, s.Orientation) > 0, "no arbitrary quaternion sign flips between resolved samples");
                    previous = s.Orientation;
                }
                Near(Math.Abs(Quaternion.Dot(q0, previous)), 1, 1e-6, "one complete turn returns to the same physical orientation");
            });
            Case("11. Gravity defines up; orientation exposes no coin result", () =>
            {
                var d = new FlightDynamics(Wide()); d.Launch(Input(velocity: new Vector3(0, 0, 3), gravity: new Vector3(0, 0, -8)));
                Check(To(d, 0.375).Phase == FlightPhase.Apex && To(d, 0.5).Phase == FlightPhase.Falling, "tilted gravity changes the motion reference axis");
                foreach (var property in typeof(FlightState).GetProperties())
                    Check(property.Name != "Heads" && property.Name != "Tails" && property.Name != "Result", "no result field in output schema");
            });
            Case("12. First-contact time is independent of sampling rate and remains terminal", () =>
            {
                double terminalTime = -1;
                foreach (double rate in new[] { 30.0, 72, 120 })
                {
                    var d = new FlightDynamics(FlightLimits.Experimental()); d.Launch(Input(velocity: new Vector3(2, 3, 0)));
                    var s = To(d, 2, rate);
                    Check(s.Status == FlightStatus.Stopped && s.StopReason == StopReason.SpatialBoundary, "horizontal envelope terminates flight");
                    if (terminalTime < 0) terminalTime = s.Elapsed;
                    else Near(s.Elapsed, terminalTime, 1e-12, "same first-contact time at all rates");
                    Near(s.Position.X, 0.75, 1e-6, "horizontal contact position");
                    var after = Record(d.Step(0.01));
                    Check(after.Position == s.Position && after.Elapsed == s.Elapsed, "stopped state does not drift");
                }
            });
            Case("13. Unsupported tiny limit scales are explicitly rejected", () =>
            {
                bool rejected = false;
                try { new FlightLimits(new Vector3(-10), new Vector3(10), maxLinearSpeed: 1e-150); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected, "1e-150 speed limit must not enter a quadratic whose discriminant underflows");
            });
            Case("14. A near-tangent miss is not rounded into a false boundary contact", () =>
            {
                var d = new FlightDynamics(new FlightLimits(new Vector3(-1e-20f, -1, -1), new Vector3(2, 1, 1), 1.5, 0.1, 10));
                d.Launch(Input(position: Vector3.UnitX, velocity: new Vector3(-2, 0, 0), gravity: new Vector3(2, 0, 0)));
                var s = To(d, 1.5);
                Check(s.StopReason == StopReason.TimeLimit, "x=(t-1)^2 never touches minX=-1e-20; stop=" + s.StopReason
                    + " elapsed=" + s.Elapsed.ToString("R", CultureInfo.InvariantCulture));
                Near(s.Elapsed, 1.5, 1e-12, "near miss remains active until lifetime limit");
            });
            using (var sha = SHA256.Create())
                Console.WriteLine("TRACE SHA256 " + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Trace.ToString()))).Replace("-", ""));
            Console.WriteLine("OFFLINE RESULT: " + _passed + "/" + _cases + " groups, assertions=" + _assertions);
            Console.WriteLine("ANALYTIC NUMERICS ONLY | NOT UNITY PHYSX | NOT RUNTIME VALIDATED | NO RNG / NO RESULT / NO CATCH");
            return _passed == _cases ? 0 : 1;
        }
    }
}
