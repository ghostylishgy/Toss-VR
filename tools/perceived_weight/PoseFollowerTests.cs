using System;
using System.Numerics;
using Toss.Experiments.PerceivedWeight;

public static class PoseFollowerTests
{
    private static int cases, failures, assertions, assertionFailures;
    private static readonly FollowPose Origin = new FollowPose(Vector3.Zero, Quaternion.Identity);
    private static readonly FollowMode[] Modes = { FollowMode.Rigid, FollowMode.SpringDamper };
    private static readonly double[] Timesteps = { 1.0 / 30, 1.0 / 240, 1.0 / 90, 1.0 / 60, 1.0 / 144, 1.0 / 120 };

    public static int Main()
    {
        cases = failures = assertions = assertionFailures = 0;
        Console.WriteLine("P1.3 ISOLATED PROTOTYPE | OFFLINE ONLY | NOT INTEGRATED | NOT RUNTIME VALIDATED");
        Case("stationary and displaced convergence; all damping regimes", Convergence);
        Case("constant linear/angular motion; 30-240 Hz", ConstantMotion);
        Case("smooth reversal preserves inertia then reverses", Reversal);
        Case("zero, tiny, maximum accepted and gap dt", TimeBoundaries);
        Case("shortest arc, +/-180 degrees and quaternion sign equivalence", Rotations);
        Case("repeat replay after Reset is deterministic", Replay);
        Case("variable timestep bounded multi-axis motion and convergence", VariableMotion);
        Case("critical analytical reference and held-target split steps", Analytical);
        Case("lag projection, channel history and stored velocity limits", Limits);
        Case("reconstruction respects sub-ULP lag caps", ReconstructionLimits);
        Case("minimum, maximum and invalid constructor parameters", Parameters);
        Case("invalid input, target, Reset and dt are atomic", InvalidInputs);
        Case("Reset clears history and matches a fresh follower", ResetHistory);
        Case("sound ON/OFF leaves both modes numerically identical", SoundIsolation);
        Console.WriteLine("OFFLINE RESULT: {0}/{1} cases passed; assertions={2}; failed assertions={3}",
            cases - failures, cases, assertions, assertionFailures);
        return failures == 0 ? 0 : 1;
    }

    private static void Case(string name, Action test)
    {
        cases++;
        int before = assertions;
        try { test(); Console.WriteLine("PASS {0} ({1} assertions)", name, assertions - before); }
        catch (Exception e) { failures++; Console.WriteLine("FAIL {0}: {1}: {2}", name, e.GetType().Name, e.Message); }
    }
    private static void Check(bool ok, string message)
    {
        assertions++;
        if (!ok) { assertionFailures++; throw new InvalidOperationException(message); }
    }
    private static void Near(double actual, double expected, double tolerance, string message)
    {
        Check(Math.Abs(actual - expected) <= tolerance,
            message + ": actual=" + actual.ToString("G17") + ", expected=" + expected.ToString("G17") + ", tolerance=" + tolerance);
    }
    private static FollowParameters Tune(double frequency = 4, double damping = 1,
        double positionLag = .02, double angularLag = .5, double linearLimit = 3, double angularLimit = 20)
    {
        return new FollowParameters(frequency, frequency, damping, damping, positionLag, angularLag, linearLimit, angularLimit);
    }
    private static PoseFollower New(FollowMode mode = FollowMode.SpringDamper, FollowParameters parameters = null,
        FollowPose? initial = null, SoundCondition sound = SoundCondition.Off, double gain = .1)
    {
        return new PoseFollower(parameters ?? Tune(), new ExperimentCondition(mode, sound, gain), initial ?? Origin);
    }
    private static FollowPose Pose(Vector3 position, Vector3 axis, double angle)
    {
        return new FollowPose(position, Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), (float)angle));
    }
    private static Quaternion Negate(Quaternion q) { return new Quaternion(-q.X, -q.Y, -q.Z, -q.W); }
    private static double Angle(Quaternion a, Quaternion b)
    {
        double an = Math.Sqrt((double)a.X * a.X + (double)a.Y * a.Y + (double)a.Z * a.Z + (double)a.W * a.W);
        double bn = Math.Sqrt((double)b.X * b.X + (double)b.Y * b.Y + (double)b.Z * b.Z + (double)b.W * b.W);
        double dot = ((double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z + (double)a.W * b.W) / (an * bn);
        double sign = dot < 0 ? -1 : 1;
        double x = a.X / an - sign * b.X / bn, y = a.Y / an - sign * b.Y / bn;
        double z = a.Z / an - sign * b.Z / bn, w = a.W / an - sign * b.W / bn;
        return 4 * Math.Asin(Math.Min(1, Math.Sqrt(x * x + y * y + z * z + w * w) / 2));
    }
    private static void PoseNear(FollowPose a, FollowPose b, double meters, double radians, string label)
    {
        Near(Vector3.Distance(a.Position, b.Position), 0, meters, label + " position (m)");
        Near(Angle(a.Rotation, b.Rotation), 0, radians, label + " rotation (rad)");
    }
    private static void SameState(PoseFollower a, PoseFollower b, string label)
    {
        Check(a.OutputPose.Position == b.OutputPose.Position && a.OutputPose.Rotation == b.OutputPose.Rotation &&
            a.LinearVelocity == b.LinearVelocity && a.AngularVelocity == b.AngularVelocity, label + " exact state");
    }
    private static FollowStep Step(PoseFollower f, FollowPose target, double dt)
    {
        FollowStep s = f.Step(f.OutputPose, target, dt);
        Check(s.Pose.Position == f.OutputPose.Position && s.Pose.Rotation == f.OutputPose.Rotation, "returned pose equals OutputPose");
        Vector3 p = s.Pose.Position;
        Check(Math.Abs(p.X) <= 1000 && Math.Abs(p.Y) <= 1000 && Math.Abs(p.Z) <= 1000, "finite coordinate bounds (m)");
        Near(s.Pose.Rotation.LengthSquared(), 1, 3e-7, "unit quaternion");
        Check(f.LinearVelocity.Length() <= f.Parameters.LinearVelocityLimit + 1e-6 &&
            f.AngularVelocity.Length() <= f.Parameters.AngularVelocityLimit + 2e-6, "finite stored velocity bounds (m/s, rad/s)");
        return s;
    }
    private static void Bounded(PoseFollower f, FollowPose target)
    {
        Check(Vector3.Distance(f.OutputPose.Position, target.Position) <= f.Parameters.MaxPositionLagMeters + 2e-7, "position lag bound (m)");
        Check(Angle(f.OutputPose.Rotation, target.Rotation) <= f.Parameters.MaxAngularLagRadians + 2e-6, "angular lag bound (rad)");
    }
    private static FollowPose Motion(double t)
    {
        return new FollowPose(new Vector3((float)(.012 * Math.Sin(1.1 * t)), (float)(.009 * Math.Sin(.7 * t)),
            (float)(.006 * (Math.Cos(.9 * t) - 1))), Quaternion.CreateFromYawPitchRoll(
            (float)(.25 * Math.Sin(.6 * t)), (float)(.2 * Math.Sin(.8 * t)), (float)(.15 * Math.Sin(1.2 * t))));
    }

    private static void Convergence()
    {
        foreach (FollowMode mode in Modes) foreach (double damping in new[] { .5, 1, 2 })
        {
            var still = new FollowPose(new Vector3(.125f, -.25f, .5f), Quaternion.Identity);
            var f = New(mode, Tune(4, damping), still);
            for (int i = 0; i < 32; i++)
            {
                Check(Step(f, still, 1.0 / 120).Flags == FollowFlags.None, "stationary flags");
                PoseNear(f.OutputPose, still, 0, 0, "stationary");
                Check(f.LinearVelocity == Vector3.Zero && f.AngularVelocity == Vector3.Zero, "stationary zero velocity");
            }
            f.Reset(Origin);
            var target = Pose(new Vector3(.006f, -.004f, .003f), new Vector3(1, 2, -3), .08);
            for (int i = 0; i < 600; i++)
            {
                Check(Step(f, target, 1.0 / 120).Flags == FollowFlags.None, "small held displacement must not limit");
                if (mode == FollowMode.Rigid) PoseNear(f.OutputPose, target, 0, 1e-7, "rigid exact target");
                else if (i == 0)
                {
                    Check(f.OutputPose.Position.Length() > 0 && f.OutputPose.Position.Length() < target.Position.Length(), "spring moves without snapping");
                    Check(Angle(f.OutputPose.Rotation, target.Rotation) > .01, "spring initial angular lag");
                }
            }
            PoseNear(f.OutputPose, target, 1e-7, 2e-6, mode + " convergence");
            Check(f.LinearVelocity.Length() < 1e-5 && f.AngularVelocity.Length() < 1e-5, "settled velocities (m/s, rad/s)");
        }
    }
    private static void ConstantMotion()
    {
        Vector3 axis = Vector3.Normalize(new Vector3(1, 2, -1));
        foreach (FollowMode mode in Modes) foreach (int hz in new[] { 30, 60, 90, 120, 144, 240 })
        {
            var f = New(mode);
            double positionSum = 0, angleSum = 0;
            for (int i = 1; i <= hz * 3; i++)
            {
                double t = (double)i / hz;
                var target = Pose(new Vector3((float)(.008 * t), 0, 0), axis, .2 * t);
                Check(Step(f, target, 1.0 / hz).Flags == FollowFlags.None, "smooth constant motion has no limiter");
                Bounded(f, target);
                if (mode == FollowMode.Rigid) PoseNear(f.OutputPose, target, 0, 1e-7, "rigid moving target");
                if (i > hz * 2)
                {
                    positionSum += Vector3.Distance(f.OutputPose.Position, target.Position);
                    angleSum += Angle(f.OutputPose.Rotation, target.Rotation);
                    if (mode == FollowMode.SpringDamper) Check(f.OutputPose.Position.X < target.Position.X, "spring trails translation");
                }
            }
            double positionLag = positionSum / hz, angularLag = angleSum / hz;
            if (mode == FollowMode.SpringDamper)
            {
                Check(positionLag > .0001 && angularLag > .003, "nonzero steady spring lag (m, rad)");
                Check(f.LinearVelocity.X > .003 && Vector3.Dot(f.AngularVelocity, axis) > .08, "moving stored velocity follows target");
            }
            Console.WriteLine("  MOTION {0} {1} Hz: mean lag={2:F4} mm, {3:F4} deg", mode, hz, positionLag * 1000, angularLag * 180 / Math.PI);
        }
    }
    private static void Reversal()
    {
        foreach (FollowMode mode in Modes)
        {
            var f = New(mode);
            FollowPose target = Origin;
            for (int i = 1; i <= 240; i++)
                Check(Step(f, Pose(new Vector3((float)(.008 * i / 240), 0, 0), Vector3.UnitY, .2 * i / 240), 1.0 / 240).Flags == FollowFlags.None, "forward no limiter");
            double firstLinear = 0, firstAngular = 0;
            for (int i = 1; i <= 120; i++)
            {
                target = Pose(new Vector3((float)(.008 * (1 - i / 240.0)), 0, 0), Vector3.UnitY, .2 * (1 - i / 240.0));
                Check(Step(f, target, 1.0 / 240).Flags == FollowFlags.None, "reversal no limiter");
                if (mode == FollowMode.Rigid) PoseNear(f.OutputPose, target, 0, 1e-7, "rigid reversal");
                else if (i == 1)
                {
                    firstLinear = f.LinearVelocity.X; firstAngular = f.AngularVelocity.Y;
                    Check(firstLinear > 0 && firstAngular > 0, "stored inertia persists on first reversed sample");
                }
            }
            if (mode == FollowMode.SpringDamper)
                Check(f.LinearVelocity.X < -.003 && f.AngularVelocity.Y < -.08, "stored motion eventually reverses");
            Console.WriteLine("  REVERSAL {0}: first stored velocity={1:G6} m/s, {2:G6} rad/s; reversed={3:G6} m/s, {4:G6} rad/s",
                mode, firstLinear, firstAngular, f.LinearVelocity.X, f.AngularVelocity.Y);
            for (int i = 0; i < 600; i++) Step(f, target, 1.0 / 120);
            PoseNear(f.OutputPose, target, 1e-7, 2e-6, "post-reversal convergence");
            Check(f.LinearVelocity.Length() < 1e-5 && f.AngularVelocity.Length() < 1e-5, "post-reversal rest");
        }
    }
    private static void TimeBoundaries()
    {
        var target = Pose(new Vector3(.005f, 0, 0), Vector3.UnitY, .04);
        foreach (FollowMode mode in Modes)
        {
            var f = New(mode);
            for (int i = 0; i < 20; i++) Step(f, target, 1.0 / 240);
            FollowPose held = f.OutputPose; Vector3 linear = f.LinearVelocity, angular = f.AngularVelocity;
            Check(Step(f, Motion(10), 0).Flags == FollowFlags.None, "zero dt flags");
            PoseNear(f.OutputPose, held, 0, 1e-7, "zero dt holds previous output");
            Check(f.LinearVelocity == linear && f.AngularVelocity == angular, "zero dt preserves velocity");
            foreach (double dt in new[] { 1e-8, 1e-10, 1e-14, 1e-20, 1e-300, double.Epsilon })
            {
                f = New(mode);
                Check(Step(f, target, dt).Flags == FollowFlags.None, "tiny dt flags");
                if (mode == FollowMode.Rigid) PoseNear(f.OutputPose, target, 0, 1e-7, "rigid positive tiny dt");
                else
                {
                    PoseNear(f.OutputPose, Origin, 1e-8, 1e-7, "tiny spring pose");
                    double omegaSquared = Math.Pow(2 * Math.PI * 4, 2);
                    Near(f.LinearVelocity.X, .005 * omegaSquared * dt, Math.Max(1e-44, .005 * omegaSquared * dt * 1e-5), "tiny analytic linear velocity (m/s)");
                    Near(f.AngularVelocity.Y, .04 * omegaSquared * dt, Math.Max(1e-44, .04 * omegaSquared * dt * 1e-5), "tiny analytic angular velocity (rad/s)");
                }
            }
            f = New(mode, Tune(1));
            Check(Step(f, target, .1).Flags == FollowFlags.None, "exact maximum dt is not a gap");
            if (mode == FollowMode.SpringDamper) Check(Vector3.Distance(f.OutputPose.Position, target.Position) > .001 && f.LinearVelocity.X > 0, "maximum dt integrates spring");
            else PoseNear(f.OutputPose, target, 0, 1e-7, "maximum dt rigid");
            foreach (double dt in new[] { .1000000001, 1, double.MaxValue })
            {
                var gapTarget = new FollowPose(new Vector3(.5f, -.2f, .1f), new Quaternion(0, 1, 0, 0));
                Check(Step(f, gapTarget, dt).Flags == FollowFlags.TimeGapReset, "gap reset flag only");
                PoseNear(f.OutputPose, gapTarget, 0, 0, "gap snaps target");
                Check(f.LinearVelocity == Vector3.Zero && f.AngularVelocity == Vector3.Zero, "gap clears velocity");
                var fresh = New(mode, f.Parameters, gapTarget);
                Step(f, gapTarget, .01); Step(fresh, gapTarget, .01); SameState(f, fresh, "gap cleared history");
            }
        }
    }
    private static void Rotations()
    {
        Vector3[] axes = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(1, 2, 3), new Vector3(-2, 1, -3) };
        foreach (FollowMode mode in Modes) foreach (Vector3 rawAxis in axes)
        {
            Vector3 axis = Vector3.Normalize(rawAxis);
            foreach (int direction in new[] { -1, 1 })
            {
                var initial = Pose(Vector3.Zero, axis, direction * 179.9 * Math.PI / 180);
                var target = Pose(Vector3.Zero, axis, -direction * 179.9 * Math.PI / 180);
                var f = New(mode, initial: initial);
                double originalError = Angle(initial.Rotation, target.Rotation);
                Check(Step(f, target, .01).Flags == FollowFlags.None, "near half-turn wrap is a small displacement");
                Check(Angle(f.OutputPose.Rotation, target.Rotation) < originalError, "shortest arc reduces wrap error");
                if (mode == FollowMode.SpringDamper)
                {
                    Check(Vector3.Dot(f.AngularVelocity, axis) * direction > 0, "correct shortest arc direction");
                    Check(Angle(f.OutputPose.Rotation, initial.Rotation) < originalError, "no long-arc jump");
                }
                for (int i = 0; i < 300; i++) Step(f, target, .01);
                PoseNear(f.OutputPose, target, 0, 2e-6, "wrap convergence");
            }
            var halfTurn = new FollowPose(Vector3.Zero, new Quaternion(axis, 0));
            var negative = new FollowPose(Vector3.Zero, Negate(halfTurn.Rotation));
            var a = New(mode); var b = New(mode); var c = New(mode, initial: new FollowPose(Vector3.Zero, Negate(Quaternion.Identity)));
            for (int i = 0; i < 200; i++)
            {
                FollowStep sa = Step(a, halfTurn, .01), sb = Step(b, negative, .01), sc = Step(c, halfTurn, .01);
                PoseNear(a.OutputPose, b.OutputPose, 0, 2e-6, "q/-q target equivalence");
                PoseNear(a.OutputPose, c.OutputPose, 0, 2e-6, "q/-q input equivalence");
                Near(Vector3.Distance(a.AngularVelocity, b.AngularVelocity), 0, 2e-6, "equivalent target angular state");
                Near(Vector3.Distance(a.AngularVelocity, c.AngularVelocity), 0, 2e-6, "equivalent input angular state");
                Check(sa.Flags == sb.Flags && sa.Flags == sc.Flags, "sign-equivalent flags");
                if (i == 0 && mode == FollowMode.SpringDamper)
                {
                    Vector3 canonical = axis.X < 0 || (axis.X == 0 && (axis.Y < 0 || (axis.Y == 0 && axis.Z < 0))) ? -axis : axis;
                    Check(Vector3.Dot(a.AngularVelocity, canonical) < 0, "exact half-turn lexicographic tie direction");
                    Check((sa.Flags & FollowFlags.LagLimited) != 0, "half-turn discontinuity projects angular lag");
                }
            }
            PoseNear(a.OutputPose, halfTurn, 0, 2e-6, "exact half-turn convergence");
            foreach (double angle in new[] { .37, Math.PI - 1e-5, Math.PI + 1e-5 })
            {
                var initial = Pose(Vector3.Zero, new Vector3(2, -3, 1), .07);
                var target = Pose(Vector3.Zero, axis, angle);
                var oppositeTarget = new FollowPose(target.Position, Negate(target.Rotation));
                var u = New(mode, initial: initial);
                var v = New(mode, initial: new FollowPose(initial.Position, Negate(initial.Rotation)));
                for (int i = 0; i < 100; i++)
                {
                    FollowStep su = Step(u, target, .01), sv = Step(v, oppositeTarget, .01);
                    PoseNear(u.OutputPose, v.OutputPose, 0, 2e-6, "general q/-q trajectory equivalence");
                    Near(Vector3.Distance(u.AngularVelocity, v.AngularVelocity), 0, 2e-6, "general q/-q angular state");
                    Check(su.Flags == sv.Flags, "general q/-q flags");
                }
            }
        }
    }
    private static void Replay()
    {
        foreach (FollowMode mode in Modes)
        {
            var f = New(mode);
            var poses = new FollowStep[512]; var linear = new Vector3[512]; var angular = new Vector3[512];
            for (int replay = 0; replay < 3; replay++)
            {
                f.Reset(Origin);
                for (int i = 0; i < poses.Length; i++)
                {
                    double dt = i % 101 == 0 ? .1001 : i % 43 == 0 ? 0 : i % 37 == 0 ? 1e-12 : Timesteps[i % Timesteps.Length];
                    FollowStep s = Step(f, Motion(i * .013), dt);
                    if (replay == 0) { poses[i] = s; linear[i] = f.LinearVelocity; angular[i] = f.AngularVelocity; }
                    else Check(s.Pose.Position == poses[i].Pose.Position && s.Pose.Rotation == poses[i].Pose.Rotation &&
                        s.Flags == poses[i].Flags && f.LinearVelocity == linear[i] && f.AngularVelocity == angular[i], "exact replay frame " + i);
                }
            }
        }
    }
    private static void VariableMotion()
    {
        foreach (FollowMode mode in Modes) foreach (double damping in new[] { .5, 1, 2 })
        {
            var f = New(mode, new FollowParameters(6, 5, damping, damping, .004, Math.PI / 36, .5, 8));
            double t = 0, maxPosition = 0, maxAngle = 0, maxLinear = 0, maxAngular = 0;
            FollowPose target = Origin;
            for (int i = 0; i < 2400; i++)
            {
                double dt = Timesteps[i % Timesteps.Length]; t += dt; target = Motion(t);
                Step(f, target, dt); Bounded(f, target);
                if (mode == FollowMode.Rigid) PoseNear(f.OutputPose, target, 0, 1e-7, "rigid multi-axis tracking");
                maxPosition = Math.Max(maxPosition, Vector3.Distance(f.OutputPose.Position, target.Position));
                maxAngle = Math.Max(maxAngle, Angle(f.OutputPose.Rotation, target.Rotation));
                maxLinear = Math.Max(maxLinear, f.LinearVelocity.Length()); maxAngular = Math.Max(maxAngular, f.AngularVelocity.Length());
            }
            for (int i = 0; i < 720; i++) { Step(f, target, 1.0 / 120); Bounded(f, target); }
            PoseNear(f.OutputPose, target, 1e-7, 2e-6, "multi-axis final convergence");
            Check(f.LinearVelocity.Length() < 1e-5 && f.AngularVelocity.Length() < 1e-5, "multi-axis final rest");
            Console.WriteLine("  VARIABLE {0} damping={1}: peak lag={2:F4} mm, {3:F4} deg; stored speed={4:F5} m/s, {5:F5} rad/s",
                mode, damping, maxPosition * 1000, maxAngle * 180 / Math.PI, maxLinear, maxAngular);
        }
    }
    private static void Analytical()
    {
        var initial = Pose(new Vector3(.006f, -.004f, .002f), Vector3.UnitZ, .08);
        var f = New(parameters: Tune(3), initial: initial);
        double t = 0, omega = 2 * Math.PI * 3;
        for (int i = 0; i < 48; i++)
        {
            double dt = Timesteps[i % Timesteps.Length]; t += dt;
            Check(Step(f, Origin, dt).Flags == FollowFlags.None, "analytical reference has no limiter");
            double displacement = (1 + omega * t) * Math.Exp(-omega * t);
            double speed = -omega * omega * t * Math.Exp(-omega * t);
            Near(Vector3.Distance(f.OutputPose.Position, initial.Position * (float)displacement), 0, 2e-8, "critical analytic position (m)");
            Near(Vector3.Distance(f.LinearVelocity, initial.Position * (float)speed), 0, 1e-6, "critical analytic velocity (m/s)");
            Near(Angle(f.OutputPose.Rotation, Quaternion.Identity), .08 * displacement, 2e-6, "critical same-axis rotation (rad)");
            Near(f.AngularVelocity.Z, .08 * speed, 1e-5, "critical same-axis angular velocity (rad/s)");
        }
        foreach (double damping in new[] { .5, 1, 2 })
        {
            var whole = New(parameters: Tune(3, damping), initial: initial);
            var split = New(parameters: Tune(3, damping), initial: initial);
            Check(Step(whole, Origin, .08).Flags == FollowFlags.None, "whole held step no limiter");
            for (int i = 0; i < 8; i++) Check(Step(split, Origin, .01).Flags == FollowFlags.None, "split held step no limiter");
            PoseNear(whole.OutputPose, split.OutputPose, 2e-8, 2e-6, "held-target split equivalence");
            Near(Vector3.Distance(whole.LinearVelocity, split.LinearVelocity), 0, 1e-6, "split linear state (m/s)");
            Near(Vector3.Distance(whole.AngularVelocity, split.AngularVelocity), 0, 1e-5, "split angular state (rad/s)");
        }
    }
    private static void Limits()
    {
        var f = New(parameters: Tune(30, .5, .002, .03, .001, .001));
        var target = Pose(new Vector3(1, -.5f, .25f), new Vector3(1, 2, 3), 1);
        FollowStep s = Step(f, target, .0001); Bounded(f, target);
        Check(s.Flags == (FollowFlags.LagLimited | FollowFlags.VelocityLimited), "projection and stored speed flags");
        Check(f.OutputPose.Position.Length() > .5, "discontinuity correction is not a per-step speed bound");
        Near(f.LinearVelocity.Length(), .001, 1e-8, "linear state cap (m/s)");
        Near(f.AngularVelocity.Length(), .001, 1e-8, "angular state cap (rad/s)");
        Console.WriteLine("  LIMIT: correction={0:F6} m in 0.0001 s; stored speed={1:F6} m/s (not displacement/dt)", f.OutputPose.Position.Length(), f.LinearVelocity.Length());
        foreach (bool positionChannel in new[] { true, false })
        {
            f = New();
            for (int i = 0; i < 12; i++) Step(f, Pose(new Vector3(.006f, 0, 0), Vector3.UnitY, .08), 1.0 / 240);
            var fresh = New(initial: f.OutputPose);
            target = positionChannel ? new FollowPose(f.OutputPose.Position + Vector3.UnitX, f.OutputPose.Rotation) : Pose(f.OutputPose.Position, Vector3.UnitY, 1);
            Check((Step(f, target, .001).Flags & FollowFlags.LagLimited) != 0, "channel projection flags");
            Step(fresh, target, .001); Bounded(f, target);
            if (positionChannel)
            {
                Near(Vector3.Distance(f.LinearVelocity, fresh.LinearVelocity), 0, 1e-7, "projection discards prior linear velocity");
                Near(Vector3.Distance(f.OutputPose.Position, fresh.OutputPose.Position), 0, 1e-7, "linear projection output ignores old velocity");
                Check(f.AngularVelocity.Length() > fresh.AngularVelocity.Length() + .01, "linear projection preserves angular history");
            }
            else
            {
                Near(Vector3.Distance(f.AngularVelocity, fresh.AngularVelocity), 0, 1e-6, "projection discards prior angular velocity");
                Near(Angle(f.OutputPose.Rotation, fresh.OutputPose.Rotation), 0, 1e-6, "angular projection output ignores old velocity");
                Check(f.LinearVelocity.Length() > fresh.LinearVelocity.Length() + .001, "angular projection preserves linear history");
            }
        }
        f = New(parameters: Tune(5, .5), initial: new FollowPose(new Vector3(999.99f, 0, 0), Quaternion.Identity));
        s = Step(f, new FollowPose(new Vector3(1000, 0, 0), Quaternion.Identity), .1);
        Check(f.OutputPose.Position.X == 1000 && f.LinearVelocity == Vector3.Zero && (s.Flags & FollowFlags.LagLimited) != 0, "coordinate-edge overshoot clamps and clears linear state");
    }
    private static void ReconstructionLimits()
    {
        foreach (float coordinate in new[] { 16f, -16f, 512f, -512f })
        {
            var target = new FollowPose(new Vector3(coordinate, 0, 0), Quaternion.Identity);
            var initial = new FollowPose(target.Position + Vector3.UnitX * Math.Sign(coordinate), Quaternion.Identity);
            var f = New(parameters: new FollowParameters(maxPositionLagMeters: 1e-6), initial: initial);
            FollowStep s = Step(f, target, .0001);
            Check((s.Flags & FollowFlags.LagLimited) != 0, "sub-ULP correction is reported");
            if (Math.Abs(coordinate) == 16)
                Check(f.OutputPose.Position == target.Position && f.LinearVelocity == Vector3.Zero, "rounding breach snaps and clears linear state");
            for (int i = 0; i < 100; i++)
            {
                double distance = Math.Abs((double)f.OutputPose.Position.X - target.Position.X);
                Check(distance <= f.Parameters.MaxPositionLagMeters, "reconstructed position obeys exact cap");
                Step(f, target, .0001);
            }
        }
        foreach (Vector3 axis in new[] { Vector3.UnitX, new Vector3(1, 2, -3) })
        {
            var target = Pose(Vector3.Zero, axis, Math.PI - 1e-6);
            var initial = new FollowPose(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, .05f) * target.Rotation);
            var f = New(parameters: new FollowParameters(maxAngularLagRadians: .0001), initial: initial);
            for (int i = 0; i < 100; i++)
            {
                Step(f, target, 1e-8);
                Check(Angle(f.OutputPose.Rotation, target.Rotation) <= .0001 + 1e-7, "reconstructed angular cap within float tolerance");
            }
        }
    }
    private static FollowParameters FromValues(double[] v)
    {
        return new FollowParameters(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7]);
    }
    private static void Reject(Action action, string label)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; }
        Check(rejected, label + " must throw ArgumentException");
    }
    private static void Parameters()
    {
        double[] minimum = { .5, .5, .5, .5, .000001, .0001, .001, .001 };
        double[] maximum = { 30, 30, 2, 2, .02, Math.PI / 6, 3, 20 };
        foreach (double[] values in new[] { minimum, maximum })
        {
            FollowParameters p = FromValues(values);
            double[] actual = { p.PositionFrequencyHz, p.RotationFrequencyHz, p.PositionDampingRatio, p.RotationDampingRatio,
                p.MaxPositionLagMeters, p.MaxAngularLagRadians, p.LinearVelocityLimit, p.AngularVelocityLimit };
            for (int i = 0; i < values.Length; i++) Near(actual[i], values[i], 0, "parameter endpoint " + i);
            foreach (FollowMode mode in Modes)
            {
                var f = New(mode, p); var target = Motion(.1);
                for (int i = 0; i < 1200; i++)
                {
                    Step(f, target, i % 2 == 0 ? .1 : 1.0 / 240);
                    Bounded(f, target);
                }
                PoseNear(f.OutputPose, target, 1e-7, 2e-6, "parameter endpoint sustained convergence");
                Check(f.LinearVelocity.Length() < 1e-5 && f.AngularVelocity.Length() < 1e-5, "parameter endpoint settled velocities");
            }
        }
        for (int i = 0; i < 8; i++) foreach (double bad in new[] { minimum[i] * .99, maximum[i] * 1.01, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            double[] values = (double[])minimum.Clone(); values[i] = bad;
            Reject(() => FromValues(values), "parameter " + i + "=" + bad);
        }
        foreach (FollowMode mode in Modes) foreach (SoundCondition sound in new[] { SoundCondition.Off, SoundCondition.On }) foreach (double gain in new[] { 0.0, 1.0 })
        {
            var condition = new ExperimentCondition(mode, sound, gain);
            Check(condition.Follow == mode && condition.Sound == sound && condition.GainNormalized == gain, "condition immutable values");
            Near(condition.EffectiveGain, sound == SoundCondition.Off ? 0 : gain, 0, "effective gain endpoint");
        }
        foreach (double bad in new[] { -.001, 1.001, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            foreach (SoundCondition sound in new[] { SoundCondition.Off, SoundCondition.On }) Reject(() => new ExperimentCondition(FollowMode.Rigid, sound, bad), "invalid gain including sound OFF");
        foreach (int bad in new[] { -1, 2, int.MaxValue })
        {
            Reject(() => new ExperimentCondition((FollowMode)bad, SoundCondition.Off), "invalid mode");
            Reject(() => new ExperimentCondition(FollowMode.Rigid, (SoundCondition)bad), "invalid sound enum");
        }
        Reject(() => new PoseFollower(null, new ExperimentCondition(FollowMode.Rigid, SoundCondition.Off), Origin), "null parameters");
        Reject(() => new PoseFollower(Tune(), null, Origin), "null condition");
    }
    private static void Atomic(PoseFollower f, Action action, string label)
    {
        FollowPose pose = f.OutputPose; Vector3 linear = f.LinearVelocity, angular = f.AngularVelocity;
        Reject(action, label);
        Check(f.OutputPose.Position == pose.Position && f.OutputPose.Rotation == pose.Rotation &&
            f.LinearVelocity == linear && f.AngularVelocity == angular, label + " leaves all state unchanged");
    }
    private static void InvalidInputs()
    {
        var invalid = new FollowPose[29]; int n = 0;
        for (int component = 0; component < 3; component++) foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 1000.01f, -1000.01f })
            invalid[n++] = new FollowPose(new Vector3(component == 0 ? value : 0, component == 1 ? value : 0, component == 2 ? value : 0), Quaternion.Identity);
        for (int component = 0; component < 4; component++) foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            invalid[n++] = new FollowPose(Vector3.Zero, new Quaternion(component == 0 ? value : 0, component == 1 ? value : 0, component == 2 ? value : 0, component == 3 ? value : 1));
        invalid[n++] = new FollowPose(Vector3.Zero, new Quaternion(0, 0, 0, 0));
        invalid[n++] = new FollowPose(Vector3.Zero, new Quaternion(0, 0, 0, 1e-8f));
        Check(n == invalid.Length, "invalid pose matrix complete");
        foreach (FollowMode mode in Modes)
        {
            var f = New(mode); var peer = New(mode); var target = Pose(new Vector3(.006f, 0, 0), Vector3.UnitY, .08);
            for (int i = 0; i < 12; i++) { Step(f, target, 1.0 / 240); Step(peer, target, 1.0 / 240); }
            foreach (FollowPose bad in invalid)
            {
                Reject(() => New(mode, initial: bad), "invalid constructor pose");
                Atomic(f, () => f.Step(bad, target, .01), "invalid input");
                Atomic(f, () => f.Step(f.OutputPose, bad, .01), "invalid target");
                Atomic(f, () => f.Reset(bad), "invalid Reset");
                Step(f, target, .001); Step(peer, target, .001); SameState(f, peer, "continuation after rejected poses");
            }
            foreach (double dt in new[] { -.001, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                Atomic(f, () => f.Step(f.OutputPose, target, dt), "invalid dt=" + dt);
                Step(f, target, .001); Step(peer, target, .001); SameState(f, peer, "continuation after rejected dt");
            }
            foreach (double dt in new[] { 0.0, .2 }) Atomic(f, () => f.Step(f.OutputPose, invalid[0], dt), "invalid target still rejected at zero/gap dt");
        }
    }
    private static void ResetHistory()
    {
        foreach (FollowMode mode in Modes)
        {
            var f = New(mode);
            for (int i = 1; i <= 30; i++) Step(f, Motion(i * .01), .01);
            if (mode == FollowMode.SpringDamper) Check(f.LinearVelocity.Length() > .001 && f.AngularVelocity.Length() > .01, "reset begins with nonzero history");
            foreach (FollowPose reset in new[] { Pose(new Vector3(.001f, -.002f, .003f), new Vector3(1, 2, -3), .06),
                new FollowPose(new Vector3(-1000, 1000, -1000), new Quaternion(2, 3, 4, 5)),
                new FollowPose(Vector3.Zero, new Quaternion(float.MaxValue, float.MaxValue, 0, float.MaxValue)),
                new FollowPose(Vector3.Zero, new Quaternion(0, 0, 0, 1e-5f)) })
            {
                f.Reset(reset); var fresh = New(mode, f.Parameters, reset);
                Check(f.LinearVelocity == Vector3.Zero && f.AngularVelocity == Vector3.Zero, "Reset zero velocities");
                PoseNear(f.OutputPose, reset, 0, 1e-7, "Reset accepts finite non-unit quaternion and coordinate endpoints");
                Near(f.OutputPose.Rotation.LengthSquared(), 1, 3e-7, "Reset normalizes quaternion");
                SameState(f, fresh, "Reset equals construction");
                for (int i = 1; i <= 32; i++)
                {
                    FollowStep a = Step(f, Motion(i * .01), .01), b = Step(fresh, Motion(i * .01), .01);
                    Check(a.Flags == b.Flags, "fresh/reset flags"); SameState(f, fresh, "fresh/reset trajectory");
                }
            }
        }
    }
    private static void SoundIsolation()
    {
        foreach (FollowMode mode in Modes) foreach (double gain in new[] { 0.0, .1, 1.0 })
        {
            var off = New(mode, new FollowParameters(), sound: SoundCondition.Off, gain: gain);
            var on = New(mode, off.Parameters, sound: SoundCondition.On, gain: gain);
            for (int i = 0; i < 720; i++)
            {
                if (i == 360) { off.Reset(Origin); on.Reset(Origin); }
                double dt = i % 109 == 0 ? .101 : i % 67 == 0 ? 0 : Timesteps[i % Timesteps.Length];
                FollowStep a = Step(off, Motion(i * .017), dt), b = Step(on, Motion(i * .017), dt);
                Check(a.Flags == b.Flags, "sound-isolated flags"); SameState(off, on, "sound-isolated pose and velocities");
            }
        }
    }
}
