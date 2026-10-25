"""P1.3 A-wrap pre-integration gate: offline doubles run + Unity .NET Standard 2.1 reference check.

Nothing is written inside the repository; artifacts go to a fresh temp directory. Unity is never started.
Usage: python tools/perceived_weight/awrap_gate/run_awrap_gate.py --unity-editor "G:/Dev/Unity/Hub/Editor/6000.0.66f2/Editor"
"""
import argparse
import hashlib
import json
import os
import subprocess
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
POSE_FOLLOWER = HERE.parent / "PoseFollower.cs"


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
    core_module = data / "Managed/UnityEngine/UnityEngine.CoreModule.dll"
    ikdasm = framework / "ikdasm.exe"
    mono_refs = [framework / (n + ".dll") for n in ("mscorlib", "System", "System.Core", "System.Numerics", "System.Numerics.Vectors")]
    sources = [POSE_FOLLOWER, HERE / "AwrapGate.cs", HERE / "AwrapGateTests.cs", HERE / "UnityNumericsBridgeSample.cs", Path(__file__).resolve()]
    for p in [mono, csc, dotnet, roslyn, netstandard, core_module, *mono_refs, *sources]:
        if not p.is_file():
            parser.error(f"Missing required file: {p}")

    before = {str(p): sha256(p) for p in sources}
    tracked = [p for p in subprocess.check_output(["git", "ls-files", "-z"], cwd=ROOT, text=True).split("\0") if p]
    tracked_before = {p: sha256(ROOT / p) for p in tracked}
    out = Path(tempfile.mkdtemp(prefix="TossVR-awrap-gate-"))
    env = dict(os.environ, TEMP=str(out), TMP=str(out), PYTHONDONTWRITEBYTECODE="1")
    print(f"ARTIFACTS {out}", flush=True)
    manifest = {"status": ["P1.3 A-WRAP FEASIBILITY GATE", "OFFLINE ONLY", "NOT INTEGRATED", "NOT RUNTIME VALIDATED"],
                "source_sha256": before, "steps": [],
                "limits": ["Doubles reproduce SDK call order only; not the SDK, not Unity, not XR input.",
                           "Unity reference check uses Unity's bundled Roslyn and reference assemblies offline; it is not a Unity Editor compile of the production project."]}

    def run(label, argv, must_pass=True):
        r = subprocess.run([str(a) for a in argv], cwd=out, env=env, capture_output=True, text=True, timeout=180)
        text = r.stdout + r.stderr
        (out / (label + ".log")).write_text(text, encoding="utf-8")
        manifest["steps"].append({"label": label, "exit_code": r.returncode})
        print(f"{label}: exit={r.returncode}", flush=True)
        print(text, end="", flush=True)
        if must_pass and r.returncode:
            raise RuntimeError(f"{label} failed")
        return r.returncode, text

    success = False
    try:
        common = [mono, csc, "/nologo", "/noconfig", "/nostdlib+", "/langversion:9.0", "/warnaserror+", "/deterministic+",
                  *[f"/reference:{p}" for p in mono_refs]]
        exe = out / "AwrapGate.Tests.exe"
        run("gate.compile", [*common, "/target:exe", f"/out:{exe}", POSE_FOLLOWER, HERE / "AwrapGate.cs", HERE / "AwrapGateTests.cs"])
        _, first = run("gate.run", [mono, exe])
        _, second = run("gate.replay", [mono, exe])
        manifest["replay_identical"] = first == second

        # Unity .NET Standard 2.1 profile: only netstandard.dll (+ UnityEngine.CoreModule for the bridge sample).
        rsp = out / "unity_netstandard.rsp"
        rsp.write_text("\n".join([
            "/nologo", "/noconfig", "/nostdlib+", "/target:library", "/langversion:9.0", "/warnaserror+", "/deterministic+",
            f'/out:"{out / "PoseFollower.unity-netstandard21.dll"}"',
            f'/reference:"{netstandard}"', f'/reference:"{core_module}"',
            f'"{POSE_FOLLOWER}"', f'"{HERE / "UnityNumericsBridgeSample.cs"}"']), encoding="utf-8")
        run("unity.netstandard21.compile", [dotnet, "exec", roslyn, f"@{rsp}"])

        # Negative control: without netstandard.dll System.Numerics must be unresolved (proves the reference provider).
        rsp_neg = out / "unity_no_netstandard.rsp"
        rsp_neg.write_text("\n".join([
            "/nologo", "/noconfig", "/nostdlib+", "/target:library", "/langversion:9.0",
            f'/out:"{out / "PoseFollower.no-netstandard.dll"}"', f'/reference:"{core_module}"', f'"{POSE_FOLLOWER}"']), encoding="utf-8")
        code, text = run("unity.negative-control.compile", [dotnet, "exec", roslyn, f"@{rsp_neg}"], must_pass=False)
        manifest["negative_control_failed_as_expected"] = code != 0 and any(c in text for c in ("CS0518", "CS0246", "CS0012"))

        code, text = run("coremodule.assemblyrefs", [mono, ikdasm, "-assemblyref", core_module], must_pass=False)
        manifest["coremodule_assemblyref_probe_exit"] = code
        manifest["coremodule_references_netstandard"] = "netstandard" in text
        success = True
    finally:
        manifest["sources_unchanged"] = all(sha256(p) == before[str(p)] for p in sources)
        manifest["tracked_changed"] = [p for p, h in tracked_before.items() if sha256(ROOT / p) != h]
        manifest["success"] = success and manifest["sources_unchanged"] and not manifest["tracked_changed"]
        (out / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        print(f"MANIFEST {out / 'manifest.json'}")
        print(f"SOURCES UNCHANGED={manifest['sources_unchanged']} TRACKED CHANGED={manifest['tracked_changed']}")
    print("P1.3 A-WRAP GATE | OFFLINE ONLY | NOT INTEGRATED | NOT RUNTIME VALIDATED")
    return 0 if manifest["success"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
