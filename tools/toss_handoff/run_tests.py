"""Compile and replay the isolated handoff with the real gate and flight sources; never start Unity."""
import argparse
import hashlib
import json
import os
import subprocess
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
LABEL = "P2 HANDOFF ISOLATED | NOT INTEGRATED | NOT RUNTIME VALIDATED | NO RNG / NO RESULT / NO CATCH"


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-editor", type=Path, required=True)
    args = parser.parse_args()
    data = args.unity_editor.resolve() / "Data"
    mono = data / "MonoBleedingEdge/bin/mono.exe"
    framework = data / "MonoBleedingEdge/lib/mono/4.5"
    csc = framework / "csc.exe"
    dotnet = data / "NetCoreRuntime/dotnet.exe"
    roslyn = data / "DotNetSdkRoslyn/csc.dll"
    netstandard = data / "NetStandard/ref/2.1.0/netstandard.dll"
    refs = [framework / (n + ".dll") for n in ("mscorlib", "System", "System.Core", "System.Numerics", "System.Numerics.Vectors")]
    gate = ROOT / "tools/toss_intent/TossIntentGate.cs"
    flight = ROOT / "tools/flight_dynamics/FlightDynamics.cs"
    handoff, tests = HERE / "TossHandoff.cs", HERE / "TossHandoffTests.cs"
    implementation = [gate, flight, handoff]
    sources = [*implementation, tests, Path(__file__).resolve()]
    for path in [mono, csc, dotnet, roslyn, netstandard, *refs, *sources]:
        if not path.is_file():
            parser.error(f"Missing required file: {path}")
    tracked = [p for p in subprocess.check_output(["git", "ls-files", "-z"], cwd=ROOT, text=True).split("\0") if p]
    before = {p: digest(ROOT / p) for p in tracked}
    source_before = {str(p): digest(p) for p in sources}
    out = Path(tempfile.mkdtemp(prefix="TossVR-toss-handoff-"))
    env = dict(os.environ, TEMP=str(out), TMP=str(out), PYTHONDONTWRITEBYTECODE="1")
    manifest = {"status": LABEL.split(" | "), "source_sha256": source_before, "steps": [],
                "limits": ["Synthetic serialized events, not SDK Unselect ordering or real ownership evidence.",
                           "Receipts prove issuance by this private gate instance, not authenticity of external hand input.",
                           "Session keys are explicit synthetic increasing numbers, not Meta SDK identifiers.",
                           "Failed resolution is terminal for that receipt; no automatic fallback or production recovery.",
                           "Same-runtime replay only; no cross-process persistence or cross-platform bitwise guarantee."]}
    print(f"ARTIFACTS {out}", flush=True)

    def run(label, argv):
        proc = subprocess.run([str(a) for a in argv], cwd=out, env=env, capture_output=True, text=True, timeout=180)
        text = proc.stdout + proc.stderr
        (out / (label + ".log")).write_text(text, encoding="utf-8")
        manifest["steps"].append({"label": label, "exit_code": proc.returncode})
        print(f"{label}: exit={proc.returncode}\n{text}", end="", flush=True)
        if proc.returncode:
            raise RuntimeError(f"{label} failed")
        return text

    success = False
    try:
        exe = out / "TossHandoff.Tests.exe"
        common = [mono, csc, "/nologo", "/noconfig", "/nostdlib+", "/langversion:9.0", "/warnaserror+", "/deterministic+",
                  *[f"/reference:{p}" for p in refs]]
        run("tests.compile", [*common, "/target:exe", f"/out:{exe}", *implementation, tests])
        first = run("tests.run", [mono, exe])
        second = run("tests.replay", [mono, exe])
        manifest["replay_identical"] = first == second
        if first != second:
            raise RuntimeError("Trace or result differs across replays")
        rsp = out / "netstandard.rsp"
        rsp.write_text("\n".join(["/nologo", "/nostdlib+", "/target:library", "/langversion:9.0", "/warnaserror+", "/deterministic+",
                                  f'/out:"{out / "TossHandoff.netstandard21.dll"}"', f'/reference:"{netstandard}"',
                                  *[f'"{p}"' for p in implementation]]), encoding="utf-8")
        run("netstandard21.compile", [dotnet, "exec", roslyn, "/noconfig", f"@{rsp}"])
        manifest["binary_sha256"] = digest(exe)
        success = True
    finally:
        manifest["sources_unchanged"] = all(digest(p) == source_before[str(p)] for p in sources)
        manifest["tracked_changed"] = [p for p, h in before.items() if digest(ROOT / p) != h]
        manifest["success"] = success and manifest["sources_unchanged"] and not manifest["tracked_changed"]
        (out / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        print(f"MANIFEST {out / 'manifest.json'}")
        print(f"SOURCES UNCHANGED={manifest['sources_unchanged']} TRACKED CHANGED={manifest['tracked_changed']}")
    print(LABEL)
    return 0 if manifest["success"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
