"""P2 Toss Intent Gate isolated prototype: compile and run the deterministic gate tests offline.

P2 TOSS INTENT ISOLATED PROTOTYPE | NOT INTEGRATED | NOT RUNTIME VALIDATED | NO RNG / NO PHYSICS FLIGHT
Uses only compilers bundled with an installed Unity Editor; Unity itself is never started. Artifacts go to a
fresh temp directory. Nothing inside the repository is written.
Usage: python tools/toss_intent/run_tests.py --unity-editor <Editor dir containing Unity.exe>
"""
import argparse
import hashlib
import json
import os
import subprocess
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
LABEL = "P2 TOSS INTENT ISOLATED PROTOTYPE | NOT INTEGRATED | NOT RUNTIME VALIDATED | NO RNG / NO PHYSICS FLIGHT"


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-editor", required=True, type=Path)
    args = parser.parse_args()
    data = args.unity_editor.resolve() / "Data"
    mono = data / "MonoBleedingEdge/bin/mono.exe"
    framework = data / "MonoBleedingEdge/lib/mono/4.5"
    csc = framework / "csc.exe"
    dotnet = data / "NetCoreRuntime/dotnet.exe"
    roslyn = data / "DotNetSdkRoslyn/csc.dll"
    netstandard = data / "NetStandard/ref/2.1.0/netstandard.dll"
    refs = [framework / (n + ".dll") for n in ("mscorlib", "System", "System.Core", "System.Numerics", "System.Numerics.Vectors")]
    gate, tests = HERE / "TossIntentGate.cs", HERE / "TossIntentTests.cs"
    sources = [gate, tests, Path(__file__).resolve()]
    for p in [mono, csc, dotnet, roslyn, netstandard, *refs, *sources]:
        if not p.is_file():
            parser.error(f"Missing required file: {p}")

    before = {str(p): sha256(p) for p in sources}
    tracked = [p for p in subprocess.check_output(["git", "ls-files", "-z"], cwd=ROOT, text=True).split("\0") if p]
    tracked_before = {p: sha256(ROOT / p) for p in tracked}
    out = Path(tempfile.mkdtemp(prefix="TossVR-toss-intent-"))
    env = dict(os.environ, TEMP=str(out), TMP=str(out), PYTHONDONTWRITEBYTECODE="1")
    print(f"ARTIFACTS {out}", flush=True)
    manifest = {"status": LABEL.split(" | "), "source_sha256": before, "steps": [],
                "limits": ["Pure .NET gate with no Unity, Meta SDK, scene, RNG or physics references.",
                           "Tests prove transition rules on synthetic deterministic input; they do not show real hands behave this way.",
                           "All thresholds are experiment parameters, not product values.",
                           "Same-runtime replay only; not a cross-platform bitwise guarantee."]}

    def run(label, argv, must_pass=True):
        r = subprocess.run([str(a) for a in argv], cwd=out, env=env, capture_output=True, text=True, timeout=180)
        text = r.stdout + r.stderr
        (out / (label + ".log")).write_text(text, encoding="utf-8")
        manifest["steps"].append({"label": label, "exit_code": r.returncode})
        print(f"{label}: exit={r.returncode}", flush=True)
        print(text, end="", flush=True)
        if must_pass and r.returncode:
            raise RuntimeError(f"{label} failed")
        return text

    success = False
    try:
        common = [mono, csc, "/nologo", "/noconfig", "/nostdlib+", "/langversion:9.0", "/warnaserror+", "/deterministic+",
                  *[f"/reference:{p}" for p in refs]]
        exe = out / "TossIntent.Tests.exe"
        run("tests.compile", [*common, "/target:exe", f"/out:{exe}", gate, tests])
        first = run("tests.run", [mono, exe])
        second = run("tests.replay", [mono, exe])
        manifest["replay_identical"] = first == second
        if first != second:
            raise RuntimeError("Output changed across identical process replays")
        rsp = out / "netstandard.rsp"
        rsp.write_text("\n".join(["/nologo", "/nostdlib+", "/target:library", "/langversion:9.0", "/warnaserror+",
                                  "/deterministic+", f'/out:"{out / "TossIntentGate.netstandard21.dll"}"',
                                  f'/reference:"{netstandard}"', f'"{gate}"']), encoding="utf-8")
        run("netstandard21.compile", [dotnet, "exec", roslyn, "/noconfig", f"@{rsp}"])
        success = True
    finally:
        manifest["sources_unchanged"] = all(sha256(p) == before[str(p)] for p in sources)
        manifest["tracked_changed"] = [p for p, h in tracked_before.items() if sha256(ROOT / p) != h]
        manifest["success"] = success and manifest["sources_unchanged"] and not manifest["tracked_changed"]
        (out / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        print(f"MANIFEST {out / 'manifest.json'}")
        print(f"SOURCES UNCHANGED={manifest['sources_unchanged']} TRACKED CHANGED={manifest['tracked_changed']}")
    print(LABEL)
    return 0 if manifest["success"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
