"""Compile actual candidate sources and run numerical tests without starting Unity or XR."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parent


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-editor", required=True, type=Path,
                        help="Installed Editor directory containing Unity.exe; only bundled compilers are used")
    args = parser.parse_args()
    data = args.unity_editor.resolve() / "Data"
    mono_root = data / "MonoBleedingEdge"
    mono = mono_root / "bin/mono.exe"
    framework = mono_root / "lib/mono/4.5"
    csc = framework / "csc.exe"
    dotnet = data / "NetCoreRuntime/dotnet.exe"
    roslyn = data / "DotNetSdkRoslyn/csc.dll"
    standard = data / "NetStandard/ref/2.1.0"
    references = [framework / (name + ".dll") for name in
                  ("mscorlib", "System", "System.Core", "System.Numerics", "System.Numerics.Vectors")]
    sources = [ROOT / "PoseFollower.cs", ROOT / "PoseFollowerTests.cs", Path(__file__).resolve()]
    for path in [mono, csc, dotnet, roslyn, standard / "netstandard.dll", *references, *sources]:
        if not path.is_file():
            parser.error(f"Missing required file: {path}")

    before = {str(path): sha256(path) for path in sources}
    output = Path(tempfile.mkdtemp(prefix="TossVR-perceived-weight-"))
    print(f"ARTIFACTS {output}", flush=True)
    manifest = {"status": ["P1.3 ISOLATED PROTOTYPE", "NOT INTEGRATED", "NOT RUNTIME VALIDATED"],
                "source_sha256": before, "commands": [],
                "limits": ["No Unity/Meta references, doubles, scene operations or native physics.",
                           "Numerical behavior is not subjective weight or audio validation.",
                           "Same-runtime replay only; not a cross-platform bitwise guarantee."]}
    env = dict(os.environ, TEMP=str(output), TMP=str(output), PYTHONDONTWRITEBYTECODE="1")

    def run(label, command):
        result = subprocess.run([str(item) for item in command], cwd=output, env=env,
                                capture_output=True, text=True, timeout=90)
        text = result.stdout + result.stderr
        (output / (label + ".log")).write_text(text, encoding="utf-8")
        manifest["commands"].append({"label": label, "argv": [str(item) for item in command],
                                     "exit_code": result.returncode})
        print(f"{label}: exit={result.returncode}", flush=True)
        if text:
            print(text, end="", flush=True)
        if result.returncode:
            raise RuntimeError(f"{label} failed; see {output}")
        return text

    success = False
    try:
        common = [mono, csc, "/nologo", "/noconfig", "/nostdlib+", "/langversion:9.0",
                  "/warnaserror+", "/deterministic+", *[f"/reference:{path}" for path in references]]
        library = output / "PerceivedWeight.dll"
        tests = output / "PerceivedWeight.Tests.exe"
        run("candidate.compile", [*common, "/target:library", f"/out:{library}", sources[0]])
        run("tests.compile", [*common, "/target:exe", f"/out:{tests}", f"/reference:{library}", sources[1]])
        standard_rsp = output / "netstandard.rsp"
        standard_rsp.write_text("\n".join([
            "/target:library", "/langversion:9.0", "/warnaserror+", "/deterministic+",
            f'/out:"{output / "PerceivedWeight.netstandard.dll"}"',
            *[f'/reference:"{path}"' for path in sorted(standard.glob("*.dll"))],
            f'"{sources[0]}"']), encoding="utf-8")
        run("netstandard.compile", [dotnet, "exec", roslyn, "/nologo", "/noconfig", "/nostdlib+",
                                     f"@{standard_rsp}"])
        first = run("numerical.first", [mono, tests])
        second = run("numerical.replay", [mono, tests])
        manifest["replay_identical"] = first == second
        if first != second:
            raise RuntimeError("Numerical output changed across identical process replays")
        success = True
    finally:
        manifest["sources_unchanged"] = all(path.is_file() and sha256(path) == before[str(path)] for path in sources)
        manifest["success"] = success and manifest["sources_unchanged"]
        (output / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    if not manifest["sources_unchanged"]:
        raise RuntimeError("Source files changed during verification")
    print("PASS: standalone compilation, .NET Standard 2.1 compilation, numerical tests and process replay")
    print("P1.3 ISOLATED PROTOTYPE | NOT INTEGRATED | NOT RUNTIME VALIDATED")


if __name__ == "__main__":
    main()
