// P1.4 ISOLATED PROTOTYPE | NOT INTEGRATED | NOT PERCEPTUALLY VALIDATED
// These cases prove rules and bounds of the solver. They do not prove that a user cannot perceive assistance.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Toss.Experiments.InvisibleGenerosity
{
    public static class GenerosityTests
    {
        private const double Dt = 1.0 / 72.0;
        private static readonly List<string> Failures = new List<string>();
        private static int _cases, _passed, _assertions;

        private sealed class Sim
        {
            public readonly GenerositySolver Solver;
            public Vector3 Coin, CoinVelocity;
            public ReferenceRegion Reference;
            public InteractionState State = InteractionState.Settling;
            public readonly List<GenerosityProposal> Log = new List<GenerosityProposal>();
            public double MaxStepVelocityDelta, MaxAssistSpeed, MinDistance = double.MaxValue;

            public Sim(AssistanceProfile profile, Vector3 coin, Vector3 center, GenerosityParameters p = null)
            {
                Solver = new GenerositySolver(profile, p ?? (profile == AssistanceProfile.Obvious
                    ? GenerosityParameters.Obvious() : GenerosityParameters.Subtle()));
                Coin = coin;
                Reference = new ReferenceRegion(center, true);
            }

            // The coin has no motion of its own; only the proposed PositionDelta is applied.
            public GenerosityProposal Tick(double dt = Dt)
            {
                var r = Solver.Step(new GenerosityInput(Coin, CoinVelocity, Reference, dt, State));
                Coin += r.PositionDelta;
                Log.Add(r);
                MaxStepVelocityDelta = Math.Max(MaxStepVelocityDelta, r.VelocityDelta.Length());
                MaxAssistSpeed = Math.Max(MaxAssistSpeed, r.AssistVelocity.Length());
                if (Reference.IsTrusted) MinDistance = Math.Min(MinDistance, (Coin - Reference.Center).Length());
                return r;
            }

            public double Run(int frames)
            {
                for (int i = 0; i < frames; i++) Tick();
                return Solver.DisplacementUsedMeters;
            }
        }

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

        private static bool Finite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsNaN(v.Y) && !float.IsNaN(v.Z)
                && !float.IsInfinity(v.X) && !float.IsInfinity(v.Y) && !float.IsInfinity(v.Z);
        }

        private static readonly Vector3 Center = new Vector3(0f, 1.0f, 0.35f);
        private static readonly Vector3 FarCoin = Center + new Vector3(0.40f, -0.05f, 0.10f);      // ~0.415 m: full gain
        private static readonly Vector3 EdgeCoin = Center + new Vector3(0.13f, 0f, 0f);             // 1 cm outside safe radius
        private static readonly Vector3 InsideCoin = Center + new Vector3(0.05f, 0.02f, 0f);

        public static int Main()
        {
            Console.WriteLine("P1.4 INVISIBLE GENEROSITY SOLVER | OFFLINE | NOT INTEGRATED | NOT PERCEPTUALLY VALIDATED");

            Case("OFF: strictly zero output, no state, input pose untouched", () =>
            {
                var sim = new Sim(AssistanceProfile.Off, FarCoin, Center);
                var start = sim.Coin;
                sim.Run(200);
                Check(sim.Coin == start, "coin position unchanged");
                Check(sim.Log.TrueForAll(r => !r.Enabled && r.PositionDelta == Vector3.Zero && r.VelocityDelta == Vector3.Zero
                    && r.Reason == AssistReason.ProfileOff), "every step zero with ProfileOff");
                Check(sim.Solver.DisplacementUsedMeters == 0 && sim.Solver.TimeUsedSeconds == 0 && !sim.Solver.EpisodeActive,
                    "no budget, no time, no episode");
            });

            Case("SUBTLE vs OBVIOUS: same scene, measurable difference, both within own bounds", () =>
            {
                var subtle = new Sim(AssistanceProfile.Subtle, FarCoin, Center);
                var obvious = new Sim(AssistanceProfile.Obvious, FarCoin, Center);
                double ds = subtle.Run(54), dob = obvious.Run(54); // 0.75 s
                Check(ds > 0.005 && ds <= 0.03 + 1e-9, "SUBTLE moved " + ds.ToString("F4") + " m within 3 cm budget");
                Check(dob > ds * 2, "OBVIOUS moved " + dob.ToString("F4") + " m, clearly more than SUBTLE");
                Check(dob <= 0.10 + 1e-9, "OBVIOUS within 10 cm budget");
                Check(subtle.MaxAssistSpeed <= 0.05 + 1e-6, "SUBTLE speed <= 0.05 m/s (" + subtle.MaxAssistSpeed.ToString("F4") + ")");
                Check(obvious.MaxAssistSpeed <= 0.15 + 1e-6, "OBVIOUS speed <= 0.15 m/s (" + obvious.MaxAssistSpeed.ToString("F4") + ")");
                Check(subtle.MaxStepVelocityDelta <= 0.30 * Dt + 1e-6, "SUBTLE per-step dv <= a*dt");
                Check(obvious.MaxStepVelocityDelta <= 1.0 * Dt + 1e-6, "OBVIOUS per-step dv <= a*dt");
                Check(subtle.Log[36].Enabled && subtle.Log[36].Reason == AssistReason.Assisting, "SUBTLE still assisting at 0.5 s");
            });

            Case("Inside safe region: no assistance, no budget consumed by motion", () =>
            {
                var sim = new Sim(AssistanceProfile.Obvious, InsideCoin, Center);
                sim.Run(72);
                Check(sim.Coin == InsideCoin, "coin unchanged inside region");
                Check(sim.Log.TrueForAll(r => !r.Enabled && r.Reason == AssistReason.InsideSafeRegion), "InsideSafeRegion every step");
                Check(sim.Solver.DisplacementUsedMeters == 0, "zero displacement used");
                Check(sim.Solver.TimeUsedSeconds > 0.9, "time still accounted while in Settling");
            });

            Case("Near boundary: weak gain, arrives at safe radius with zero injected velocity, never crosses", () =>
            {
                var sim = new Sim(AssistanceProfile.Obvious, EdgeCoin, Center);
                var first = sim.Tick();
                Check(first.Gain > 0 && first.Gain < 0.02, "gain small 1 cm outside (" + first.Gain.ToString("F4") + ")");
                Check((first.Limits & AssistLimits.OvershootGuard) != 0 || first.AssistVelocity.Length() < 0.15,
                    "braking bound engaged near boundary");
                sim.Run(300);
                Check(sim.MinDistance >= 0.12 - 1e-6, "never inside safe radius by assistance alone (" + sim.MinDistance.ToString("F5") + ")");
                var last = sim.Log[sim.Log.Count - 1];
                Check(last.Reason == AssistReason.InsideSafeRegion || last.Reason == AssistReason.TimeBudgetExhausted
                    || last.AssistVelocity.Length() < 1e-3, "ends at rest at boundary or budget-stopped (" + last.Reason + ")");
                var far = new Sim(AssistanceProfile.Obvious, FarCoin, Center);
                var farFirst = far.Tick();
                Check(farFirst.Gain == 1 && farFirst.AssistVelocity.Length() > first.AssistVelocity.Length(),
                    "further away -> stronger assistance");
            });

            Case("Target sudden change: direction flips, injected velocity stays acceleration-continuous", () =>
            {
                var sim = new Sim(AssistanceProfile.Obvious, FarCoin, Center);
                sim.Run(20);
                var before = sim.Log[19].AssistVelocity;
                sim.Reference = new ReferenceRegion(FarCoin + new Vector3(-0.3f, 0.25f, 0f), true);
                var after = sim.Tick();
                Check((after.AssistVelocity - before).Length() <= 1.0 * Dt + 1e-6, "one-step velocity change bounded by a*dt");
                sim.Run(15);
                Check(sim.MaxStepVelocityDelta <= 1.0 * Dt + 1e-6, "continuity holds across the jump (" + sim.MaxStepVelocityDelta.ToString("F5") + ")");
                Check(Vector3.Dot(sim.Log[sim.Log.Count - 1].AssistVelocity, sim.Reference.Center - sim.Coin) > 0, "now moving toward the new target");
            });

            Case("Target unavailable: never invents a target", () =>
            {
                var none = new Sim(AssistanceProfile.Obvious, FarCoin, Center);
                none.Reference = ReferenceRegion.None;
                none.Run(72);
                Check(none.Coin == FarCoin && none.Log.TrueForAll(r => !r.Enabled && r.Reason == AssistReason.NoTrustedTarget),
                    "untrusted reference -> zero, NoTrustedTarget");
                Check(none.Solver.TimeUsedSeconds > 0.9, "episode time still accounted while the target is untrusted");
            });

            Case("Hard disable: unsafe conditions stop assistance at once, no ramp toward the old target", () =>
            {
                // Each mutation makes the world unverifiable mid-episode; the solver must abandon its injected velocity
                // immediately instead of braking along the stale direction.
                var hard = new (string name, Action<Sim> mutate, AssistReason reason)[]
                {
                    ("target untrusted", s => s.Reference = ReferenceRegion.None, AssistReason.NoTrustedTarget),
                    ("target non-finite", s => s.Reference = new ReferenceRegion(new Vector3(float.NaN, 0f, 0f), true), AssistReason.NoTrustedTarget),
                    ("coin position non-finite", s => s.Coin = new Vector3(float.PositiveInfinity, 1f, 0f), AssistReason.InvalidInput),
                    ("coin velocity non-finite", s => s.CoinVelocity = new Vector3(0f, float.NaN, 0f), AssistReason.InvalidInput),
                    ("grabbed", s => s.State = InteractionState.Grabbed, AssistReason.NotInSettleWindow),
                    ("cancelled", s => s.State = InteractionState.Cancelled, AssistReason.NotInSettleWindow),
                    ("recovery locked", s => s.State = InteractionState.RecoveryLocked, AssistReason.NotInSettleWindow),
                };
                foreach (var h in hard)
                {
                    var sim = new Sim(AssistanceProfile.Obvious, FarCoin, Center);
                    sim.Run(20);
                    Check(sim.Solver.AssistVelocity.Length() > 0.1, h.name + ": assisting at full speed before the failure");
                    h.mutate(sim);
                    var coinAtFailure = sim.Coin;
                    var r = sim.Tick();
                    Check(!r.Enabled && r.PositionDelta == Vector3.Zero && r.VelocityDelta == Vector3.Zero,
                        h.name + ": no displacement proposed on the failure frame (" + r.Reason + ", |dp|=" + r.PositionDelta.Length().ToString("F5") + ")");
                    Check(r.Reason == h.reason, h.name + ": reason " + r.Reason);
                    Check(r.AssistVelocity == Vector3.Zero && sim.Solver.AssistVelocity == Vector3.Zero, h.name + ": injected velocity dropped, not braked");
                    Check((r.Limits & AssistLimits.HardDisabled) != 0, h.name + ": HardDisabled reported");
                    sim.Run(10);
                    Check(sim.Coin == coinAtFailure, h.name + ": coin never moved toward the old target after the failure");
                }

                // Regaining a trusted target restarts from zero velocity, not from the stale one.
                var regain = new Sim(AssistanceProfile.Obvious, FarCoin, Center);
                regain.Run(20);
                regain.Reference = ReferenceRegion.None; regain.Tick();
                regain.Reference = new ReferenceRegion(Center, true);
                var back = regain.Tick();
                Check(back.Enabled && back.Reason == AssistReason.Assisting && back.AssistVelocity.Length() <= 1.0 * Dt + 1e-6,
                    "regained target: ramps up again from zero");
            });

            Case("Soft braking: normal end conditions ramp down with bounded deceleration", () =>
            {
                // Time budget: assistance ends with a short ramp (Enabled frames with dv <= a*dt), not a cut.
                var p = GenerosityParameters.Obvious();
                var bigBudget = new GenerosityParameters(p.SafeRadiusMeters, p.FullGainRadiusMeters, p.MaxAssistRangeMeters,
                    p.MaxAssistSpeed, p.MaxAssistAcceleration, 10, p.TimeBudgetSeconds, p.LowEnergySpeedMax);
                var timed = new Sim(AssistanceProfile.Obvious, FarCoin, Center, bigBudget);
                timed.Run((int)(p.TimeBudgetSeconds * 72));
                var hit = timed.Tick();
                Check(hit.Reason == AssistReason.TimeBudgetExhausted && hit.Enabled && (hit.Limits & AssistLimits.HardDisabled) == 0,
                    "time budget: first exhausted frame still ramping (" + hit.Reason + ", enabled=" + hit.Enabled + ")");
                Check(hit.VelocityDelta.Length() <= p.MaxAssistAcceleration * Dt + 1e-6, "time budget: ramp-down step bounded by a*dt");
                int rampFrames = 0;
                while (timed.Solver.AssistVelocity != Vector3.Zero && rampFrames < 100) { timed.Tick(); rampFrames++; }
                Check(rampFrames > 1 && rampFrames < 100, "time budget: reached zero over " + rampFrames + " frames");
                Check(timed.MaxStepVelocityDelta <= p.MaxAssistAcceleration * Dt + 1e-6, "time budget: continuity preserved through the ramp");

                // Displacement budget: braking bound already brought velocity to zero; no HardDisabled flag anywhere.
                var longTime = new GenerosityParameters(p.SafeRadiusMeters, p.FullGainRadiusMeters, p.MaxAssistRangeMeters,
                    p.MaxAssistSpeed, p.MaxAssistAcceleration, p.DisplacementBudgetMeters, 100, p.LowEnergySpeedMax);
                var spent = new Sim(AssistanceProfile.Obvious, Center + new Vector3(0.55f, 0f, 0f), Center, longTime);
                spent.Run(72 * 3);
                Check(spent.Log.TrueForAll(r => (r.Limits & AssistLimits.HardDisabled) == 0), "displacement budget: never a hard disable");
                Check(spent.Solver.AssistVelocity == Vector3.Zero && spent.MaxStepVelocityDelta <= p.MaxAssistAcceleration * Dt + 1e-6,
                    "displacement budget: settled to zero with bounded deceleration");
            });

            Case("Grabbed / Cancelled / RecoveryLocked / Settled / Showcase: zero and injected velocity dropped", () =>
            {
                foreach (var state in new[] { InteractionState.Grabbed, InteractionState.Cancelled, InteractionState.RecoveryLocked,
                             InteractionState.Settled, InteractionState.Showcase })
                {
                    var sim = new Sim(AssistanceProfile.Obvious, FarCoin, Center);
                    sim.Run(20);
                    Check(sim.Solver.AssistVelocity != Vector3.Zero, state + ": assisting before state change");
                    sim.State = state;
                    var r = sim.Tick();
                    Check(!r.Enabled && r.PositionDelta == Vector3.Zero && r.Reason == AssistReason.NotInSettleWindow, state + ": zero output");
                    Check(sim.Solver.AssistVelocity == Vector3.Zero && !sim.Solver.EpisodeActive, state + ": injected velocity dropped, episode closed");
                    sim.Run(5);
                    Check(sim.Solver.AssistVelocity == Vector3.Zero, state + ": stays zero while not settling");
                }
            });

            Case("Zero and abnormal deltaTime: no output, no impulse accumulation", () =>
            {
                var sim = new Sim(AssistanceProfile.Obvious, FarCoin, Center);
                sim.Run(20);
                var v = sim.Solver.AssistVelocity;
                double used = sim.Solver.DisplacementUsedMeters, time = sim.Solver.TimeUsedSeconds;
                var z = sim.Tick(0);
                Check(!z.Enabled && z.Reason == AssistReason.InvalidDeltaTime && sim.Solver.AssistVelocity == v
                    && sim.Solver.DisplacementUsedMeters == used && sim.Solver.TimeUsedSeconds == time, "dt=0: zero, state unchanged");
                var neg = sim.Tick(-Dt);
                Check(!neg.Enabled && neg.Reason == AssistReason.InvalidDeltaTime && sim.Solver.AssistVelocity == v, "dt<0: zero, state unchanged");
                var nan = sim.Tick(double.NaN);
                Check(!nan.Enabled && nan.Reason == AssistReason.InvalidDeltaTime, "dt=NaN: zero");
                var inf = sim.Tick(double.PositiveInfinity);
                Check(!inf.Enabled && inf.Reason == AssistReason.InvalidDeltaTime, "dt=Inf: zero");
                var gap = sim.Tick(0.5);
                Check(!gap.Enabled && gap.Reason == AssistReason.TimeGap && sim.Solver.AssistVelocity == Vector3.Zero
                    && sim.Solver.DisplacementUsedMeters == used, "dt=0.5: TimeGap, velocity dropped, no 0.5 s of motion injected");
                var resume = sim.Tick();
                Check(resume.Enabled && resume.AssistVelocity.Length() <= 1.0 * Dt + 1e-6, "resumes from zero after gap");
            });

            Case("NaN / Infinity inputs: zero output, finite always", () =>
            {
                var sim = new Sim(AssistanceProfile.Obvious, FarCoin, Center);
                sim.Run(10);
                var bad = new[]
                {
                    new GenerosityInput(new Vector3(float.NaN, 1f, 0f), Vector3.Zero, sim.Reference, Dt, InteractionState.Settling),
                    new GenerosityInput(FarCoin, new Vector3(0f, float.PositiveInfinity, 0f), sim.Reference, Dt, InteractionState.Settling),
                    new GenerosityInput(FarCoin, Vector3.Zero, new ReferenceRegion(new Vector3(float.NegativeInfinity, 0f, 0f), true), Dt, InteractionState.Settling),
                    new GenerosityInput(new Vector3(5000f, 0f, 0f), Vector3.Zero, sim.Reference, Dt, InteractionState.Settling),
                };
                foreach (var input in bad)
                {
                    var r = sim.Solver.Step(input);
                    Check(Finite(r.PositionDelta) && Finite(r.VelocityDelta) && Finite(r.AssistVelocity), "finite output for invalid input");
                    Check(r.Reason == AssistReason.InvalidInput || r.Reason == AssistReason.NoTrustedTarget, "invalid input rejected (" + r.Reason + ")");
                    Check(!r.Enabled && r.AssistVelocity == Vector3.Zero, "invalid input hard-disables (" + r.Reason + ")");
                }
                Check(!double.IsNaN(sim.Solver.DisplacementUsedMeters) && !double.IsNaN(sim.Solver.TimeUsedSeconds), "budgets finite");
            });

            Case("Reset and new episode: no history carried", () =>
            {
                var sim = new Sim(AssistanceProfile.Obvious, FarCoin, Center);
                sim.Run(30);
                Check(sim.Solver.DisplacementUsedMeters > 0 && sim.Solver.AssistVelocity != Vector3.Zero, "history present");
                sim.Solver.Reset();
                Check(sim.Solver.DisplacementUsedMeters == 0 && sim.Solver.TimeUsedSeconds == 0 && sim.Solver.AssistVelocity == Vector3.Zero
                    && !sim.Solver.EpisodeActive, "Reset clears everything");
                var fresh = new Sim(AssistanceProfile.Obvious, sim.Coin, Center);
                var a = sim.Tick(); var b = fresh.Tick();
                Check(a.PositionDelta == b.PositionDelta && a.AssistVelocity == b.AssistVelocity, "post-Reset step identical to fresh solver");

                var cycle = new Sim(AssistanceProfile.Obvious, FarCoin, Center);
                cycle.Run(30);
                double used = cycle.Solver.DisplacementUsedMeters;
                cycle.State = InteractionState.Grabbed; cycle.Tick();
                cycle.State = InteractionState.Settling;
                var r = cycle.Tick();
                Check(cycle.Solver.DisplacementUsedMeters < used && cycle.Solver.TimeUsedSeconds < 2 * Dt,
                    "new Settling episode starts with a fresh budget (per-release budget, see risk notes)");
                Check(r.AssistVelocity.Length() <= 1.0 * Dt + 1e-6, "new episode starts from zero injected velocity");
            });

            Case("Long run: displacement budget is a hard cap and exit is safe", () =>
            {
                foreach (var profile in new[] { AssistanceProfile.Subtle, AssistanceProfile.Obvious })
                {
                    var p = profile == AssistanceProfile.Subtle ? GenerosityParameters.Subtle() : GenerosityParameters.Obvious();
                    // Generous time budget so the displacement budget is the binding limit; target far enough that it is never reached.
                    var longTime = new GenerosityParameters(p.SafeRadiusMeters, p.FullGainRadiusMeters, p.MaxAssistRangeMeters,
                        p.MaxAssistSpeed, p.MaxAssistAcceleration, p.DisplacementBudgetMeters, 100, p.LowEnergySpeedMax);
                    var sim = new Sim(profile, Center + new Vector3(0.55f, 0f, 0f), Center, longTime);
                    sim.Run(72 * 10);
                    Check(sim.Solver.DisplacementUsedMeters <= p.DisplacementBudgetMeters + 1e-9,
                        profile + ": used " + sim.Solver.DisplacementUsedMeters.ToString("F5") + " <= budget " + p.DisplacementBudgetMeters);
                    Check((sim.Coin - (Center + new Vector3(0.55f, 0f, 0f))).Length() <= p.DisplacementBudgetMeters + 1e-6,
                        profile + ": coin moved no more than the budget");
                    Check(sim.Solver.AssistVelocity == Vector3.Zero, profile + ": injected velocity exactly zero after exhaustion");
                    var last = sim.Log[sim.Log.Count - 1];
                    Check(!last.Enabled && last.Reason == AssistReason.DisplacementBudgetExhausted, profile + ": reports DisplacementBudgetExhausted (" + last.Reason + ")");
                    Check(sim.MaxStepVelocityDelta <= p.MaxAssistAcceleration * Dt + 1e-6, profile + ": ramp to exhaustion stayed acceleration-continuous");
                    Check(sim.Log.TrueForAll(r => (r.Limits & AssistLimits.HardClamped) == 0), profile + ": braking bound made the hard clamp unnecessary");

                    var bigBudget = new GenerosityParameters(p.SafeRadiusMeters, p.FullGainRadiusMeters, p.MaxAssistRangeMeters,
                        p.MaxAssistSpeed, p.MaxAssistAcceleration, 10, p.TimeBudgetSeconds, p.LowEnergySpeedMax);
                    var timed = new Sim(profile, FarCoin, Center, bigBudget);
                    timed.Run((int)(p.TimeBudgetSeconds * 72) + 72);
                    var l2 = timed.Log[timed.Log.Count - 1];
                    Check(!l2.Enabled && l2.Reason == AssistReason.TimeBudgetExhausted && timed.Solver.AssistVelocity == Vector3.Zero,
                        profile + ": time budget ends assistance with zero velocity");
                }
            });

            Case("Continuity and no-overshoot invariants over a full approach", () =>
            {
                var sim = new Sim(AssistanceProfile.Obvious, FarCoin, Center);
                sim.Run(72 * 3);
                var p = sim.Solver.Parameters;
                Check(sim.MaxStepVelocityDelta <= p.MaxAssistAcceleration * Dt + 1e-6, "|dv| per step <= a*dt everywhere");
                Check(sim.MaxAssistSpeed <= p.MaxAssistSpeed + 1e-6, "|v| <= vmax everywhere");
                Check(sim.MinDistance >= p.SafeRadiusMeters - 1e-6, "never crossed into safe radius (" + sim.MinDistance.ToString("F5") + ")");
                bool towardOnly = true;
                var pos = FarCoin;
                foreach (var r in sim.Log)
                {
                    if (r.Enabled && Vector3.Dot(r.PositionDelta, Center - pos) < 0) towardOnly = false;
                    pos += r.PositionDelta;
                }
                Check(towardOnly, "every applied delta points toward the reference (no bounce)");
                // Monotone speed envelope: speed rises, plateaus, then only decreases.
                bool decreasingPhase = false, monotone = true;
                for (int i = 1; i < sim.Log.Count; i++)
                {
                    double a = sim.Log[i - 1].AssistVelocity.Length(), b = sim.Log[i].AssistVelocity.Length();
                    if (b < a - 1e-6) decreasingPhase = true;
                    else if (decreasingPhase && b > a + 1e-6) monotone = false;
                }
                Check(monotone, "speed envelope rise -> plateau -> decay without re-acceleration");
            });

            Case("High-energy release is not a settle candidate", () =>
            {
                var sim = new Sim(AssistanceProfile.Obvious, FarCoin, Center);
                sim.CoinVelocity = new Vector3(0f, 0f, 1.2f);
                sim.Run(20);
                Check(sim.Coin == FarCoin && sim.Log.TrueForAll(r => !r.Enabled && r.Reason == AssistReason.HighEnergy), "HighEnergy -> zero");
                sim.CoinVelocity = new Vector3(0f, 0f, 0.2f);
                var r1 = sim.Tick();
                Check(r1.Enabled && r1.Reason == AssistReason.Assisting, "resumes when coin slows into the low-energy window");
                var far = new Sim(AssistanceProfile.Obvious, Center + new Vector3(0.8f, 0f, 0f), Center);
                far.Run(5);
                Check(far.Log.TrueForAll(r => !r.Enabled && r.Reason == AssistReason.OutOfAssistRange), "beyond assist range -> zero");
            });

            Console.WriteLine("OFFLINE RESULT: " + _passed + "/" + _cases + " cases passed, assertions=" + _assertions
                + ", failed assertions=" + Failures.Count);
            Console.WriteLine("RULES AND BOUNDS ONLY | NOT A PERCEPTION RESULT | P1.4 ISOLATED PROTOTYPE | NOT INTEGRATED");
            return Failures.Count == 0 ? 0 : 1;
        }
    }
}
